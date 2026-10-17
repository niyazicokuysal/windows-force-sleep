namespace ForceSleep.Tests;

public class PowerCfgTests
{
    private const string Requests = """
        DISPLAY:
        [PROCESS] \Device\HarddiskVolume3\Program Files\Google\Chrome\Application\chrome.exe
        Video Wake Lock

        SYSTEM:
        [DRIVER] Realtek High Definition Audio (HDAUDIO\FUNC_01&VEN_10EC&DEV_0897&SUBSYS_1458A0C3&REV_1000\5&2a5b7e1c&0&0001)
        An audio stream is currently in use.
        [SERVICE] \Device\HarddiskVolume3\Windows\System32\svchost.exe (wuauserv)

        AWAYMODE:
        [PROCESS] \Device\HarddiskVolume3\Program Files (x86)\Media\MediaCenter.exe

        EXECUTION:
        [PROCESS] \Device\HarddiskVolume3\Users\x\AppData\Roaming\Spotify\Spotify.exe
        Playing audio

        PERFBOOST:
        None.

        ACTIVELOCKSCREEN:
        None.

        """;

    [Fact]
    public void ParseRequests_ReadsEveryEntryWithItsCategoryAndReason()
    {
        var requests = PowerCfg.ParseRequests(Requests);

        Assert.Equal(
            [
                new PowerRequest(RequestKind.Display, "Google Chrome", "Video Wake Lock", "PROCESS", "chrome.exe"),
                new PowerRequest(RequestKind.System, "Sürücü: Realtek High Definition Audio", "An audio stream is currently in use.",
                    "DRIVER", @"Realtek High Definition Audio (HDAUDIO\FUNC_01&VEN_10EC&DEV_0897&SUBSYS_1458A0C3&REV_1000\5&2a5b7e1c&0&0001)"),
                new PowerRequest(RequestKind.System, "Windows hizmeti: wuauserv", null, "SERVICE", "wuauserv"),
                new PowerRequest(RequestKind.AwayMode, "MediaCenter", null, "PROCESS", "MediaCenter.exe"),
                new PowerRequest(RequestKind.Execution, "Spotify", "Playing audio", "PROCESS", "Spotify.exe"),
            ],
            requests);
    }

    [Fact]
    public void ElevatedChild_RejectsCommandsOutsideTheAllowList()
    {
        // Must fail without running anything: only wake/override verbs may run elevated.
        var payload = Convert.ToBase64String(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
            new[] { new[] { "/hibernate", "off" }, new[] { "/setactive", "SCHEME_MAX" }, Array.Empty<string>() }));

        Assert.Equal(3, PowerCfg.RunAsElevatedChild(payload));
    }

    [Fact]
    public void ElevatedChild_RejectsMalformedPayload()
    {
        Assert.Equal(1, PowerCfg.RunAsElevatedChild("not base64 & calc.exe"));
    }

    [Fact]
    public void ParseRequests_ReturnsNothingWhenAllCategoriesAreEmpty()
    {
        Assert.Empty(PowerCfg.ParseRequests("DISPLAY:\r\nNone.\r\n\r\nSYSTEM:\r\nNone.\r\n"));
    }

    [Fact]
    public void ParseRequests_ReturnsNothingForTheAdminRequiredMessage()
    {
        Assert.Empty(PowerCfg.ParseRequests(
            "This command requires administrator privileges and must be executed from an elevated command prompt."));
    }

    [Fact]
    public void ParseWakeTimers_ReadsOwnerAndScheduledTask()
    {
        var timers = PowerCfg.ParseWakeTimers("""
            Timer set by [SERVICE] \Device\HarddiskVolume3\Windows\System32\svchost.exe (SystemEventsBroker) expires at 3:00:24 AM on 10/18/2026.
              Reason: Windows will execute 'NT TASK\Microsoft\Windows\UpdateOrchestrator\Universal Orchestrator Start' scheduled task that requested waking the computer.
            """);

        Assert.Equal(
            [new WakeTimer("Windows hizmeti: SystemEventsBroker", "Zamanlanmış görev: Universal Orchestrator Start (Windows Update)")],
            timers);
    }

    [Fact]
    public void ParseWakeTimers_ReturnsNothingWhenThereAreNone()
    {
        Assert.Empty(PowerCfg.ParseWakeTimers("There are no active wake timers in the system."));
    }

    [Theory]
    [InlineData("HID Keyboard Device (004)", "Klavye")]
    [InlineData("HID-compliant mouse (007)", "Fare")]
    [InlineData("HID uyumlu fare", "Fare")]
    [InlineData("Realtek PCIe 2.5GbE Family Controller #2", "Ağ kartı")]
    [InlineData("Intel(R) Wi-Fi 6 AX201 160MHz", "Ağ kartı")]
    [InlineData("USB Root Hub (USB 3.0)", "USB aygıtı")]
    [InlineData("Some Unknown Device", "Some Unknown Device")]
    public void FriendlyDevice_UsesEverydayWords(string device, string expected)
    {
        Assert.Equal(expected, PowerCfg.FriendlyDevice(device));
    }
}
