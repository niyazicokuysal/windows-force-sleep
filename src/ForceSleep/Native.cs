using System.Runtime.InteropServices;

namespace ForceSleep;

internal static partial class Native
{
    // POWER_ACTION / SYSTEM_POWER_STATE values from winnt.h
    public const int PowerActionSleep = 2;
    public const int PowerSystemSleeping1 = 2;

    // POWER_ACTION_* flags from winnt.h
    public const uint PowerActionOverrideApps = 0x00000004;
    public const uint PowerActionCritical = 0x80000000;

    private const int SeShutdownPrivilege = 19;

    [LibraryImport("powrprof.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool SetSuspendState(
        [MarshalAs(UnmanagedType.U1)] bool hibernate,
        [MarshalAs(UnmanagedType.U1)] bool forceCritical,
        [MarshalAs(UnmanagedType.U1)] bool disableWakeEvent);

    [LibraryImport("powrprof.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool GetPwrCapabilities([Out] byte[] capabilities);

    // Undocumented but stable since NT 5; lets us request a critical sleep that skips
    // the query phase where apps and drivers can veto the transition.
    [LibraryImport("ntdll.dll")]
    public static partial int NtInitiatePowerAction(
        int systemAction,
        int lightestSystemState,
        uint flags,
        [MarshalAs(UnmanagedType.U1)] bool asynchronous);

    [LibraryImport("ntdll.dll")]
    private static partial int RtlAdjustPrivilege(
        int privilege,
        [MarshalAs(UnmanagedType.U1)] bool enable,
        [MarshalAs(UnmanagedType.U1)] bool currentThread,
        [MarshalAs(UnmanagedType.U1)] out bool wasEnabled);

    [LibraryImport("ntdll.dll")]
    public static partial uint RtlNtStatusToDosError(int status);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool QueryUnbiasedInterruptTime(out ulong unbiasedTime);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetLastInputInfo(ref LastInputInfo info);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(IntPtr icon);

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerGetActiveScheme(IntPtr rootPowerKey, out IntPtr activeScheme);

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerSetActiveScheme(IntPtr rootPowerKey, IntPtr scheme);

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerReadACValueIndex(IntPtr rootPowerKey, IntPtr scheme, in Guid subGroup, in Guid setting, out uint value);

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerReadDCValueIndex(IntPtr rootPowerKey, IntPtr scheme, in Guid subGroup, in Guid setting, out uint value);

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerWriteACValueIndex(IntPtr rootPowerKey, IntPtr scheme, in Guid subGroup, in Guid setting, uint value);

    [LibraryImport("powrprof.dll")]
    public static partial uint PowerWriteDCValueIndex(IntPtr rootPowerKey, IntPtr scheme, in Guid subGroup, in Guid setting, uint value);

    [LibraryImport("kernel32.dll")]
    public static partial IntPtr LocalFree(IntPtr memory);

    // Window messages and power broadcast events.
    public const int WM_POWERBROADCAST = 0x0218;
    public const int WM_HOTKEY = 0x0312;
    public const int PBT_APMSUSPEND = 0x0004;
    public const int PBT_APMRESUMESUSPEND = 0x0007;
    public const int PBT_APMRESUMEAUTOMATIC = 0x0012;
    public const int PBT_POWERSETTINGCHANGE = 0x8013;

    public static readonly Guid GUID_CONSOLE_DISPLAY_STATE = new("6fe69556-704a-47a0-8f24-c28d936fda47");
    public static readonly Guid GUID_SYSTEM_AWAYMODE = new("98a7f580-01f7-48aa-9c0f-44352c29e5c0");

    [LibraryImport("user32.dll", SetLastError = true)]
    public static partial IntPtr RegisterPowerSettingNotification(IntPtr recipient, in Guid powerSetting, int flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterPowerSettingNotification(IntPtr handle);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(IntPtr window, int id);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PostMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(IntPtr window);

    // EXECUTION_STATE flags.
    public const uint ES_CONTINUOUS = 0x80000000;
    public const uint ES_SYSTEM_REQUIRED = 0x00000001;

    [LibraryImport("kernel32.dll")]
    public static partial uint SetThreadExecutionState(uint flags);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetSystemPowerStatus(out SystemPowerStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    /// <summary>True when running on battery; desktops always report AC.</summary>
    public static bool OnBattery() => GetSystemPowerStatus(out var status) && status.ACLineStatus == 0;

    /// <summary>How long since the last keyboard or mouse input.</summary>
    public static TimeSpan IdleTime() => TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - LastInputTick()));

    /// <summary>Turns all monitors off; moving the mouse turns them back on.</summary>
    public static void TurnScreenOff()
    {
        const uint WM_SYSCOMMAND = 0x0112;
        const int SC_MONITORPOWER = 0xF170;
        const int MonitorOff = 2;
        PostMessageW(new IntPtr(0xFFFF), WM_SYSCOMMAND, SC_MONITORPOWER, MonitorOff);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    /// <summary>Sleep APIs require SeShutdownPrivilege to be enabled on the process token.</summary>
    public static bool EnableShutdownPrivilege() =>
        RtlAdjustPrivilege(SeShutdownPrivilege, enable: true, currentThread: false, out _) >= 0;

    /// <summary>Tick count (ms) of the last keyboard or mouse input in this session.</summary>
    public static uint LastInputTick()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        return GetLastInputInfo(ref info) ? info.Time : 0;
    }
}
