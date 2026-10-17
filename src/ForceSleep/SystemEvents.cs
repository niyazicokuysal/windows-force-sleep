using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ForceSleep;

/// <summary>
/// A hidden top-level window that receives power broadcasts (display on/off, away mode,
/// suspend/resume) and the global hotkey. Top-level, because message-only windows don't get
/// broadcasts like resume.
/// </summary>
internal sealed class SystemEvents : IDisposable
{
    private const int HotkeyId = 1;

    private readonly HwndSource _window;
    private readonly IntPtr _displayNotification;
    private readonly IntPtr _awayModeNotification;
    private bool _hotkeyRegistered;

    public event Action<bool>? DisplayOffChanged;
    public event Action<bool>? AwayModeChanged;
    public event Action? Suspending;
    /// <summary>Resumed, whether or not anyone is there (sent for every wake).</summary>
    public event Action? Resumed;
    public event Action? HotkeyPressed;

    public bool DisplayOff { get; private set; }
    public bool AwayMode { get; private set; }

    public SystemEvents()
    {
        _window = new HwndSource(new HwndSourceParameters("ForceSleep.Events") { Width = 0, Height = 0, WindowStyle = 0 });
        _window.AddHook(WndProc);
        _displayNotification = Native.RegisterPowerSettingNotification(_window.Handle, Native.GUID_CONSOLE_DISPLAY_STATE, 0);
        _awayModeNotification = Native.RegisterPowerSettingNotification(_window.Handle, Native.GUID_SYSTEM_AWAYMODE, 0);
    }

    /// <summary>Registers the shortcut; false if another app already owns it.</summary>
    public bool SetHotkey(Hotkey? hotkey)
    {
        if (_hotkeyRegistered)
        {
            Native.UnregisterHotKey(_window.Handle, HotkeyId);
            _hotkeyRegistered = false;
        }
        if (hotkey is null)
            return true;

        _hotkeyRegistered = Native.RegisterHotKey(_window.Handle, HotkeyId, hotkey.NativeModifiers, hotkey.VirtualKey);
        Log.Write($"Hotkey {hotkey}: {(_hotkeyRegistered ? "registered" : "already in use")}");
        return _hotkeyRegistered;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == Native.WM_HOTKEY && wParam == HotkeyId)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        else if (message == Native.WM_POWERBROADCAST)
        {
            switch ((int)wParam)
            {
                case Native.PBT_POWERSETTINGCHANGE:
                    OnPowerSettingChange(lParam);
                    break;
                case Native.PBT_APMSUSPEND:
                    Suspending?.Invoke();
                    break;
                case Native.PBT_APMRESUMEAUTOMATIC:
                    Resumed?.Invoke();
                    break;
            }
        }
        return IntPtr.Zero;
    }

    private void OnPowerSettingChange(IntPtr lParam)
    {
        // POWERBROADCAST_SETTING: GUID PowerSetting; DWORD DataLength; UCHAR Data[].
        var setting = Marshal.PtrToStructure<Guid>(lParam);
        var value = Marshal.ReadInt32(lParam, 20);

        if (setting == Native.GUID_CONSOLE_DISPLAY_STATE)
        {
            // 0 = off, 1 = on, 2 = dimmed.
            DisplayOff = value == 0;
            DisplayOffChanged?.Invoke(DisplayOff);
        }
        else if (setting == Native.GUID_SYSTEM_AWAYMODE)
        {
            AwayMode = value == 1;
            Log.Write($"Away mode {(AwayMode ? "entered" : "left")}");
            AwayModeChanged?.Invoke(AwayMode);
        }
    }

    public void Dispose()
    {
        SetHotkey(null);
        Native.UnregisterPowerSettingNotification(_displayNotification);
        Native.UnregisterPowerSettingNotification(_awayModeNotification);
        _window.Dispose();
    }
}
