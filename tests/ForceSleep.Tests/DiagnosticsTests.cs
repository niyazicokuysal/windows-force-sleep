namespace ForceSleep.Tests;

public class DiagnosticsTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 16, 0, 0);

    private static DiagnosticsReport Build(
        SleepAttempt? attempt = null,
        IReadOnlyList<SleepRecord>? sleeps = null,
        IReadOnlyList<string>? devices = null,
        IReadOnlyList<PowerRequest>? requests = null,
        PowerSettingValue? awayMode = null,
        PowerSettingValue? wakeTimers = null,
        IReadOnlyList<AppliedFix>? applied = null) =>
        DiagnosticsReport.Build(new DiagnosticsInput(
            attempt, sleeps ?? [], devices ?? [], requests, null, awayMode, wakeTimers, applied ?? [], Now));

    private static PowerRequest AwayModeRequest =>
        new(RequestKind.AwayMode, "MediaCenter", null, "PROCESS", "MediaCenter.exe");

    [Fact]
    public void AttemptHistory_ReadsOnlyTheLastAttempt()
    {
        var attempt = AttemptHistory.Last(
        [
            "2026-10-17 15:00:00  Sleep requested",
            "2026-10-17 15:00:12  Normal: Slept (3600 s)",
            "2026-10-17 15:53:30  Sleep requested",
            "2026-10-17 15:53:42  Normal: TimedOut",
            "2026-10-17 15:53:43  NtInitiatePowerAction failed, status 0xC0000022 (error 5)",
            "2026-10-17 15:53:43  Forced: Refused",
            "2026-10-17 15:53:50  Hibernate: Slept (120 s)",
        ]);

        Assert.NotNull(attempt);
        Assert.Equal(new DateTime(2026, 10, 17, 15, 53, 30), attempt.At);
        Assert.Equal(
            [
                (SleepMethod.Normal, StepOutcome.TimedOut),
                (SleepMethod.Forced, StepOutcome.Refused),
                (SleepMethod.Hibernate, StepOutcome.Slept),
            ],
            attempt.Steps);
    }

    [Fact]
    public void AttemptHistory_IgnoresLogsWrittenBeforeAttemptsWereMarked()
    {
        Assert.Null(AttemptHistory.Last(["2026-10-17 15:53:44  Normal: Slept"]));
    }

    [Fact]
    public void SleepHistory_ParsesWakeEventByFieldName()
    {
        const string xml = """
            <Event xmlns="http://schemas.microsoft.com/win/2004/08/events/event">
              <EventData>
                <Data Name="SleepTime">2026-10-17T12:53:32.1776893Z</Data>
                <Data Name="WakeTime">2026-10-17T12:53:44.7767857Z</Data>
                <Data Name="WakeSourceText">HID-compliant mouse (009)</Data>
                <Data Name="WakeTimerOwner"></Data>
              </EventData>
            </Event>
            """;

        var record = SleepHistory.Parse(xml);

        Assert.NotNull(record);
        Assert.Equal("HID-compliant mouse (009)", record.WakeSource);
        Assert.Null(record.WakeTimerOwner);
        Assert.InRange(record.Duration.TotalSeconds, 12, 13);
    }

    [Fact]
    public void SleepList_ExplainsTimerWakeInPlainWords()
    {
        var sleep = new SleepRecord(Now.AddHours(-3), Now.AddHours(-1),
            "Windows will execute 'NT TASK\\Microsoft\\Windows\\UpdateOrchestrator\\Schedule Wake To Work' scheduled task that requested waking the computer.",
            "\\Device\\HarddiskVolume5\\Windows\\System32\\svchost.exe",
            "SystemEventsBroker");

        var report = Build(sleeps: [sleep]);

        var line = Assert.Single(report.Sections.Single(s => s.Title == "Son uykular").Lines);
        Assert.EndsWith(
            "2 saat 0 dakika uyudu · Uyandıran: zamanlayıcı — Zamanlanmış görev: Schedule Wake To Work (Windows Update), " +
            "Windows hizmeti: SystemEventsBroker",
            line.Text);
    }

    [Fact]
    public void Summary_PrefersAwayModeRequestBecauseItExplainsScreenOffButRunning()
    {
        var report = Build(requests: [AwayModeRequest], awayMode: new PowerSettingValue(1, 0));

        Assert.Contains("Uzakta modu", report.Summary);
        Assert.Contains("MediaCenter", report.Summary);
    }

    [Fact]
    public void Summary_PointsOutAwayModeSettingEvenWithoutAScan()
    {
        var report = Build(awayMode: new PowerSettingValue(1, 0));

        Assert.StartsWith("\"Uzakta modu\" açık.", report.Summary);
    }

    [Fact]
    public void Summary_ExplainsForcedSleep()
    {
        var attempt = new SleepAttempt(Now.AddMinutes(-5),
            [(SleepMethod.Normal, StepOutcome.TimedOut), (SleepMethod.Forced, StepOutcome.Slept)]);

        var report = Build(attempt: attempt);

        Assert.Equal("Son denemede normal uyku engellendi; zorla uyku işe yaradı.", report.Summary);
    }

    [Fact]
    public void Summary_PointsOutAShortSleepAndWhatWokeIt()
    {
        var sleep = new SleepRecord(Now.AddSeconds(-20), Now.AddSeconds(-8), "HID-compliant mouse (009)", null);

        var report = Build(sleeps: [sleep], awayMode: PowerSettingValue.Off);

        Assert.Equal("Bilgisayar son uykudan 12 saniye sonra uyandı. Uyandıran: Fare (HID-compliant mouse (009)).",
            report.Summary);
    }

    [Fact]
    public void WakeDevices_OfferToDisableEachGroupAndWarnAboutNetworkCard()
    {
        var report = Build(devices:
            ["HID-compliant mouse (007)", "HID-compliant mouse (009)", "Realtek PCIe 2.5GbE Family Controller #2"]);

        var section = report.Sections.Single(s => s.Title == "Bilgisayarı uyandırabilen aygıtlar");
        Assert.Equal(["Fare (2 aygıt)", "Ağ kartı: Realtek PCIe 2.5GbE Family Controller #2"], section.Lines.Select(l => l.Text));
        Assert.Contains("Ağ kartının", section.Hint);

        var mice = Assert.IsType<DisableDeviceWakeFix>(section.Lines[0].Action?.Fix);
        Assert.Equal(["HID-compliant mouse (007)", "HID-compliant mouse (009)"], mice.Devices);
        Assert.True(mice.NeedsConfirmation);
        Assert.False(Assert.IsType<DisableDeviceWakeFix>(section.Lines[1].Action?.Fix).NeedsConfirmation);
    }

    [Fact]
    public void Settings_OfferToTurnOffWhatIsOn()
    {
        var report = Build(awayMode: new PowerSettingValue(1, 0), wakeTimers: PowerSettingValue.Off);

        var section = report.Sections.Single(s => s.Title == "Windows ayarları");
        Assert.Equal(new TurnOffSettingFix(PowerSetting.AwayMode), section.Lines[0].Action?.Fix);
        Assert.Equal("Uyandırma zamanlayıcıları: kapalı", section.Lines[1].Text);
        Assert.Null(section.Lines[1].Action);
    }

    [Fact]
    public void Requests_AlreadyIgnoredAreHiddenAndListedAsUndoable()
    {
        var chrome = new PowerRequest(RequestKind.Display, "Google Chrome", null, "PROCESS", "chrome.exe");
        var applied = new AppliedFix(FixKind.RequestIgnored, "MediaCenter.exe", "MediaCenter", "PROCESS", ["AWAYMODE"]);

        var report = Build(requests: [AwayModeRequest, chrome], applied: [applied]);

        var requests = report.Sections.Single(s => s.Title == "Uyanık kalmak isteyen programlar");
        var line = Assert.Single(requests.Lines);
        Assert.Equal(new IgnoreRequestFix(chrome), line.Action?.Fix);

        var changes = report.Sections.Single(s => s.Title == "Force Sleep ile yaptığın değişiklikler");
        Assert.Equal(new UndoFix(applied), Assert.Single(changes.Lines).Action?.Fix);
        Assert.DoesNotContain("Uzakta modu\" isteği", report.Summary);
    }
}
