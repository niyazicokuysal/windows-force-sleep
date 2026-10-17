using System.Text;

namespace ForceSleep;

/// <summary>A one-click action shown as a link next to a report line.</summary>
internal sealed record ReportAction(string Label, Fix Fix);

internal sealed record ReportLine(string Text, ReportAction? Action = null)
{
    public static implicit operator ReportLine(string text) => new(text);
}

internal sealed record ReportSection(string Title, IReadOnlyList<ReportLine> Lines, string? Hint = null);

/// <summary>Everything the report is built from; gathered by <see cref="DiagnosticsReport.Gather"/>.</summary>
internal sealed record DiagnosticsInput(
    SleepAttempt? Attempt,
    IReadOnlyList<SleepRecord> Sleeps,
    IReadOnlyList<string> WakeDevices,
    IReadOnlyList<PowerRequest>? Requests,
    IReadOnlyList<WakeTimer>? Timers,
    PowerSettingValue? AwayMode,
    PowerSettingValue? WakeTimerSetting,
    IReadOnlyList<AppliedFix> Applied,
    DateTime Now);

/// <summary>The "Why didn't it sleep?" answer, in plain words.</summary>
internal sealed record DiagnosticsReport(string Summary, IReadOnlyList<ReportSection> Sections)
{
    // A sleep this short was almost certainly not what the user wanted.
    public static readonly TimeSpan ShortSleep = TimeSpan.FromMinutes(2);

    public static DiagnosticsInput Gather(ElevatedScan? scan, Settings settings) => new(
        AttemptHistory.Last(),
        SleepHistory.Recent(5),
        PowerCfg.WakeArmedDevices(),
        scan is null ? null : PowerCfg.ParseRequests(scan.Requests),
        scan is null ? null : PowerCfg.ParseWakeTimers(scan.WakeTimers),
        PowerSettings.Read(PowerSetting.AwayMode),
        PowerSettings.Read(PowerSetting.WakeTimers),
        settings.AppliedFixes.ToList(),
        DateTime.Now);

    public static DiagnosticsReport Build(DiagnosticsInput input)
    {
        var sections = new List<ReportSection>();

        // A scan taken before a fix still lists what the fix silenced.
        var requests = input.Requests?
            .Where(r => r.Kind != RequestKind.Other)
            .Where(r => !input.Applied.Any(a =>
                Fixes.IsOverrideFor(a, r.CallerType, r.OverrideName) && a.Requests?.Contains(r.RequestType) == true))
            .ToList();

        if (input.Attempt is not null)
            sections.Add(new ReportSection(L.LastAttemptTitle, DescribeAttempt(input.Attempt)));

        sections.Add(input.Sleeps.Count == 0
            ? new ReportSection(L.SleepsTitle, [L.NoSleepsRecorded])
            : new ReportSection(L.SleepsTitle, input.Sleeps.Select(s => (ReportLine)DescribeSleep(s)).ToList(),
                input.Sleeps.Any(s => s.WakeTimerOwner is not null) ? L.TimerSleepHint : null));

        sections.Add(DescribeWakeDevices(input.WakeDevices));

        if (DescribeSettings(input) is { } settingsSection)
            sections.Add(settingsSection);

        if (requests is not null)
        {
            sections.Add(new ReportSection(
                L.RequestsTitle,
                requests.Count == 0
                    ? [L.NoRequests]
                    : requests.Select(r => new ReportLine(DescribeRequest(r),
                        new ReportAction(L.Ignore, new IgnoreRequestFix(r)))).ToList(),
                requests.Count == 0 ? null : L.RequestsHint));
        }

        if (input.Timers is not null)
        {
            sections.Add(new ReportSection(
                L.TimersTitle,
                input.Timers.Count == 0
                    ? [L.NoTimers]
                    : input.Timers.Select(t => (ReportLine)(t.Reason is null ? t.Owner : $"{t.Owner} — {t.Reason}")).ToList()));
        }

        if (input.Applied.Count > 0)
        {
            sections.Add(new ReportSection(
                L.ChangesTitle,
                input.Applied.Select(a => new ReportLine(DescribeApplied(a), new ReportAction(L.Undo, new UndoFix(a)))).ToList()));
        }

        return new DiagnosticsReport(Summarize(input, requests), sections);
    }

    private static string Summarize(DiagnosticsInput input, IReadOnlyList<PowerRequest>? requests)
    {
        var awayModeCallers = requests?.Where(r => r.Kind == RequestKind.AwayMode).Select(r => r.Source).Distinct().ToList();
        if (awayModeCallers is { Count: > 0 } && input.AwayMode?.IsOn != false)
            return L.SummaryAwayModeRequest(string.Join(", ", awayModeCallers));

        if (input.Attempt is { Steps.Count: > 0 } attempt && input.Now - attempt.At < TimeSpan.FromDays(7))
        {
            switch (attempt.Steps[^1])
            {
                case (SleepMethod.Forced, StepOutcome.Slept):
                    return L.SummaryForced;
                case (SleepMethod.Hibernate, StepOutcome.Slept) when attempt.Steps.Count > 1:
                    return L.SummaryHibernated;
                case (_, StepOutcome.Refused or StepOutcome.TimedOut):
                    return L.SummaryFailed;
            }
        }

        if (input.AwayMode?.IsOn == true)
            return L.SummaryAwayModeOn;

        if (input.Sleeps.Count == 0)
            return L.SummaryNoSleeps;

        var last = input.Sleeps[0];
        if (last.Duration < ShortSleep)
            return L.SummaryShortSleep(L.Duration(last.Duration), WakeCause(last));

        return L.SummaryAllGood;
    }

    private static List<ReportLine> DescribeAttempt(SleepAttempt attempt)
    {
        var lines = new List<ReportLine> { L.AttemptTime(attempt.At) };
        lines.AddRange(attempt.Steps.Select(s => (ReportLine)$"{L.Method(s.Method)}: {L.Outcome(s.Outcome)}"));
        if (attempt.Steps.Count == 0)
            lines.Add(L.AttemptIncomplete);
        return lines;
    }

    private static string DescribeSleep(SleepRecord sleep)
    {
        var text = L.SleptFor(sleep.SleptAt, L.Duration(sleep.Duration));
        return WakeCause(sleep) is { } cause ? text + L.WokenBy(cause) : text;
    }

    private static string? WakeCause(SleepRecord sleep)
    {
        if (sleep.WakeTimerOwner is { } owner)
        {
            var who = PowerCfg.FriendlyProgram(owner, sleep.WakeTimerContext);
            return L.Timer(PowerCfg.FriendlyTimerReason(sleep.WakeSource) is { } why ? $"{why}, {who}" : who);
        }
        if (sleep.WakeSource is { } source)
        {
            var friendly = PowerCfg.FriendlyDevice(source);
            return friendly == source ? source : $"{friendly} ({source})";
        }
        return null;
    }

    private static ReportSection DescribeWakeDevices(IReadOnlyList<string> devices)
    {
        if (devices.Count == 0)
            return new ReportSection(L.WakeDevicesTitle, [L.NoWakeDevices]);

        var lines = devices
            .GroupBy(PowerCfg.ClassifyDevice)
            .SelectMany(g => g.Key == DeviceKind.Other
                // Unrecognized devices have nothing in common, so list them one by one.
                ? g.Select(d => (Text: d, Devices: (IReadOnlyList<string>)[d]))
                : [(
                    Text: g.Key switch
                    {
                        DeviceKind.Network => $"{L.Device(g.Key)}: {string.Join(", ", g)}",
                        _ when g.Count() > 1 => L.DeviceCount(L.Device(g.Key), g.Count()),
                        _ => L.Device(g.Key),
                    },
                    Devices: (IReadOnlyList<string>)g.ToList())])
            .Select(x => new ReportLine(x.Text, new ReportAction(L.DisableWake, new DisableDeviceWakeFix(x.Devices))))
            .ToList();

        var hints = new List<string>();
        if (devices.Any(PowerCfg.IsNetworkDevice))
            hints.Add(L.NetworkWakeHint);
        if (devices.Any(d => PowerCfg.ClassifyDevice(d) == DeviceKind.Mouse))
            hints.Add(L.MouseWakeHint);

        return new ReportSection(L.WakeDevicesTitle, lines, hints.Count == 0 ? null : string.Join(" ", hints));
    }

    private static ReportSection? DescribeSettings(DiagnosticsInput input)
    {
        var lines = new List<ReportLine>();

        if (input.AwayMode is { } awayMode)
            lines.Add(awayMode.IsOn
                ? new ReportLine(L.AwayModeOn, new ReportAction(L.TurnOff, new TurnOffSettingFix(PowerSetting.AwayMode)))
                : L.AwayModeOff);

        if (input.WakeTimerSetting is { } timers)
            lines.Add(timers.IsOn
                ? new ReportLine(L.WakeTimersOn, new ReportAction(L.TurnOff, new TurnOffSettingFix(PowerSetting.WakeTimers)))
                : L.WakeTimersOff);

        return lines.Count == 0 ? null : new ReportSection(L.SettingsTitle, lines);
    }

    private static string DescribeRequest(PowerRequest request)
    {
        var text = $"{request.Source} — {L.RequestWhat(request.Kind)}";
        return request.Reason is null ? text : $"{text} ({request.Reason})";
    }

    private static string DescribeApplied(AppliedFix applied)
    {
        switch (applied.Kind)
        {
            case FixKind.DeviceWakeDisabled:
                // Named at display time so the text follows the current UI language.
                var friendly = PowerCfg.FriendlyDevice(applied.Target);
                return L.CannotWake(friendly == applied.Target ? applied.Target : $"{friendly} ({applied.Target})");
            case FixKind.RequestIgnored:
                return L.RequestIgnored(applied.Label, string.Join(", ", applied.Requests ?? []));
            default:
                return Enum.TryParse<PowerSetting>(applied.Target, out var setting)
                    ? L.SettingTurnedOff(L.SettingName(setting))
                    : L.SettingTurnedOff(applied.Label);
        }
    }

    public override string ToString()
    {
        var text = new StringBuilder().AppendLine(Summary);
        foreach (var section in Sections)
        {
            text.AppendLine().AppendLine(section.Title);
            foreach (var line in section.Lines)
                text.AppendLine(line.Action is null ? $"  • {line.Text}" : $"  • {line.Text}  [{line.Action.Label}]");
            if (section.Hint is not null)
                text.AppendLine($"  ({section.Hint})");
        }
        return text.ToString();
    }
}
