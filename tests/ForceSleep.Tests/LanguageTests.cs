using System.Reflection;

namespace ForceSleep.Tests;

public sealed class LanguageTests : IDisposable
{
    public void Dispose() => L.Turkish = true;

    [Fact]
    public void English_ReportIsEnglish()
    {
        L.Turkish = false;
        var sleep = new SleepRecord(new DateTime(2026, 10, 17, 15, 53, 32), new DateTime(2026, 10, 17, 15, 53, 44),
            "HID-compliant mouse (009)", null);

        var report = DiagnosticsReport.Build(new DiagnosticsInput(null, [sleep], ["Realtek PCIe 2.5GbE Family Controller #2"],
            null, null, PowerSettingValue.Off, null, [], new DateTime(2026, 10, 17, 16, 0, 0)));

        Assert.Equal("The PC woke up 12 seconds after its last sleep. Woken by: Mouse (HID-compliant mouse (009)).", report.Summary);
        Assert.Equal("Oct 17 15:53 — slept 12 seconds · Woken by: Mouse (HID-compliant mouse (009))",
            report.Sections.Single(s => s.Title == "Recent sleeps").Lines.Single().Text);
        Assert.Equal("Network card: Realtek PCIe 2.5GbE Family Controller #2",
            report.Sections.Single(s => s.Title == "Devices that can wake the PC").Lines.Single().Text);
    }

    [Fact]
    public void English_FriendlyNames()
    {
        L.Turkish = false;

        Assert.Equal("Keyboard", PowerCfg.FriendlyDevice("HID Keyboard Device (004)"));
        Assert.Equal("Windows service: wuauserv",
            PowerCfg.FriendlySource(@"[SERVICE] \Device\HarddiskVolume3\Windows\System32\svchost.exe (wuauserv)"));
    }

    [Fact]
    public void EveryTextDiffersBetweenLanguages()
    {
        // Catches a string that was added in Turkish but never translated (or vice versa).
        var properties = typeof(L)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string))
            .ToList();

        L.Turkish = true;
        var turkish = properties.ToDictionary(p => p.Name, p => (string)p.GetValue(null)!);
        L.Turkish = false;
        var untranslated = properties.Where(p => (string)p.GetValue(null)! == turkish[p.Name]).Select(p => p.Name).ToList();

        Assert.Empty(untranslated);
    }
}
