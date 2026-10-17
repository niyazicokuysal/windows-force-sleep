using System.Windows.Input;

namespace ForceSleep.Tests;

public class HotkeyAndWakeTests
{
    [Theory]
    [InlineData("Ctrl+Alt+End", ModifierKeys.Control | ModifierKeys.Alt, Key.End)]
    [InlineData("win+shift+S", ModifierKeys.Windows | ModifierKeys.Shift, Key.S)]
    [InlineData(" Ctrl + F12 ", ModifierKeys.Control, Key.F12)]
    public void Hotkey_ParsesWhatItWrites(string text, ModifierKeys modifiers, Key key)
    {
        var hotkey = Hotkey.Parse(text);

        Assert.Equal(new Hotkey(modifiers, key), hotkey);
        Assert.Equal(hotkey, Hotkey.Parse(hotkey!.ToString()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("End")]            // no modifier: would fire while typing
    [InlineData("Shift+A")]        // Shift alone is still typing
    [InlineData("Ctrl+Alt")]       // no key
    [InlineData("Ctrl+A+B")]       // two keys
    [InlineData("Ctrl+NoSuchKey")]
    public void Hotkey_RejectsUnsafeOrInvalid(string text)
    {
        Assert.Null(Hotkey.Parse(text));
    }

    [Fact]
    public void Hotkey_MapsToWindowsCodes()
    {
        var hotkey = Hotkey.Parse(Hotkey.Default)!;

        Assert.Equal(0x23u, hotkey.VirtualKey); // VK_END
        Assert.Equal(0x1u | 0x2u | 0x4000u, hotkey.NativeModifiers); // Alt | Control | NoRepeat
    }

    [Fact]
    public void UnexpectedWake_TimerAndNetworkCount()
    {
        var now = DateTime.Now;
        var timer = new SleepRecord(now.AddHours(-5), now, "Windows will execute 'NT TASK\\Microsoft\\Windows\\UpdateOrchestrator\\Reboot' scheduled task",
            "\\Device\\HarddiskVolume5\\Windows\\System32\\svchost.exe", "SystemEventsBroker");
        var network = new SleepRecord(now.AddHours(-5), now, "Realtek PCIe 2.5GbE Family Controller #2", null);

        Assert.Contains("Windows Update", AppController.UnexpectedWakeCause(timer));
        Assert.Contains("Realtek", AppController.UnexpectedWakeCause(network));
    }

    [Theory]
    [InlineData("HID Keyboard Device (004)")]
    [InlineData("HID-compliant mouse (009)")]
    [InlineData("Power Button")]
    [InlineData("Some Unknown Device")]
    [InlineData(null)]
    public void UnexpectedWake_UserInputAndUnknownStayQuiet(string? source)
    {
        var now = DateTime.Now;
        Assert.Null(AppController.UnexpectedWakeCause(new SleepRecord(now.AddHours(-5), now, source, null)));
    }
}
