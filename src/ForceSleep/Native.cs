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
