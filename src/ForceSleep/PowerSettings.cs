namespace ForceSleep;

internal enum PowerSetting
{
    /// <summary>When on, apps can turn "sleep" into screen-off-but-running.</summary>
    AwayMode,
    /// <summary>Lets programs schedule the machine to wake up.</summary>
    WakeTimers,
}

/// <summary>A power plan setting's index when plugged in (AC) and on battery (DC). 0 means off.</summary>
internal readonly record struct PowerSettingValue(uint Ac, uint Dc)
{
    public static readonly PowerSettingValue Off = new(0, 0);

    public bool IsOn => Ac != 0 || Dc != 0;
}

/// <summary>
/// Reads and writes settings of the active power plan. Standard users may change their plan's
/// settings, so this needs no admin rights.
/// </summary>
internal static class PowerSettings
{
    private static readonly Guid SleepGroup = new("238c9fa8-0aad-41ed-83f4-97be242c8f20");
    private static readonly Guid AwayModeId = new("25dfa149-5dd1-4736-b5ab-e8a37b5b8187");
    private static readonly Guid WakeTimersId = new("bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d");
    private static readonly Guid SleepAfterId = new("29f6c1db-86da-48c5-9fdb-f2b67b1f44da");

    /// <summary>
    /// "Sleep after" from the active power plan for the current power source, or null for never.
    /// </summary>
    public static TimeSpan? WindowsSleepTimeout()
    {
        var seconds = WithActiveScheme(scheme =>
        {
            uint value;
            var ok = Native.OnBattery()
                ? Native.PowerReadDCValueIndex(IntPtr.Zero, scheme, SleepGroup, SleepAfterId, out value)
                : Native.PowerReadACValueIndex(IntPtr.Zero, scheme, SleepGroup, SleepAfterId, out value);
            return ok == 0 ? value : 0u;
        });
        return seconds == 0 ? null : TimeSpan.FromSeconds(seconds);
    }

    public static PowerSettingValue? Read(PowerSetting setting)
    {
        var id = Id(setting);
        return WithActiveScheme(scheme =>
            Native.PowerReadACValueIndex(IntPtr.Zero, scheme, SleepGroup, id, out var ac) == 0
            && Native.PowerReadDCValueIndex(IntPtr.Zero, scheme, SleepGroup, id, out var dc) == 0
                ? new PowerSettingValue(ac, dc)
                : (PowerSettingValue?)null);
    }

    public static bool Write(PowerSetting setting, PowerSettingValue value)
    {
        var id = Id(setting);
        var ok = WithActiveScheme(scheme =>
            Native.PowerWriteACValueIndex(IntPtr.Zero, scheme, SleepGroup, id, value.Ac) == 0
            && Native.PowerWriteDCValueIndex(IntPtr.Zero, scheme, SleepGroup, id, value.Dc) == 0
            // Written values only take effect once the plan is re-applied.
            && Native.PowerSetActiveScheme(IntPtr.Zero, scheme) == 0);

        Log.Write($"Set {setting} to AC={value.Ac} DC={value.Dc}: {(ok ? "ok" : "failed")}");
        return ok;
    }

    private static Guid Id(PowerSetting setting) => setting switch
    {
        PowerSetting.AwayMode => AwayModeId,
        PowerSetting.WakeTimers => WakeTimersId,
        _ => throw new ArgumentOutOfRangeException(nameof(setting)),
    };

    private static T? WithActiveScheme<T>(Func<IntPtr, T> action)
    {
        if (Native.PowerGetActiveScheme(IntPtr.Zero, out var scheme) != 0)
            return default;
        try
        {
            return action(scheme);
        }
        finally
        {
            Native.LocalFree(scheme);
        }
    }
}
