namespace ForceSleep;

/// <summary>Why Force Sleep decided to put the machine to sleep on its own.</summary>
internal enum AutoSleepReason
{
    /// <summary>Windows turned a sleep into away mode: screen off, but still running.</summary>
    AwayMode,
    /// <summary>Windows' own "sleep after" time passed long ago but the machine is still awake.</summary>
    WindowsDidNotSleep,
    /// <summary>The user's own idle-sleep setting.</summary>
    Idle,
}

internal sealed record AutoSleepSettings(bool Watchdog, bool IdleSleep, TimeSpan IdleSleepAfter, bool WaitForMedia);

/// <summary>A snapshot of everything the decision depends on.</summary>
internal sealed record PolicyInput(
    AutoSleepSettings Settings,
    TimeSpan Idle,
    bool DisplayOff,
    bool AwayMode,
    TimeSpan? WindowsSleepTimeout,
    bool MediaPlaying,
    bool KeepAwake,
    TimeSpan SinceResume,
    TimeSpan SinceLastAutoAttempt);

/// <summary>Decides when to sleep automatically. Pure, so every rule is unit tested.</summary>
internal static class SleepPolicy
{
    /// <summary>After pressing Sleep the user walks away; this long without input confirms it.</summary>
    public static readonly TimeSpan AwayModeGrace = TimeSpan.FromSeconds(30);

    /// <summary>Windows gets this much extra time past its own timeout before we step in.</summary>
    public static readonly TimeSpan WindowsGrace = TimeSpan.FromMinutes(2);

    /// <summary>A timer or update may have woken the machine on purpose; let it work for a while.</summary>
    public static readonly TimeSpan ResumeGrace = TimeSpan.FromMinutes(5);

    /// <summary>Don't retry in a loop if an automatic sleep didn't work.</summary>
    public static readonly TimeSpan RetryCooldown = TimeSpan.FromMinutes(10);

    public static AutoSleepReason? Decide(PolicyInput input)
    {
        if (input.KeepAwake || input.SinceLastAutoAttempt < RetryCooldown)
            return null;

        var waitForMedia = input.Settings.WaitForMedia && input.MediaPlaying;

        // Away mode means someone asked for sleep and didn't get it, so act quickly.
        if (input.Settings.Watchdog && input.AwayMode && input.Idle >= AwayModeGrace && !waitForMedia)
            return AutoSleepReason.AwayMode;

        if (input.SinceResume < ResumeGrace || waitForMedia)
            return null;

        // Only when the screen is off: a lit screen means something is deliberately being shown.
        if (input.Settings.Watchdog && input.DisplayOff
            && input.WindowsSleepTimeout is { } timeout && input.Idle >= timeout + WindowsGrace)
            return AutoSleepReason.WindowsDidNotSleep;

        if (input.Settings.IdleSleep && input.Idle >= input.Settings.IdleSleepAfter)
            return AutoSleepReason.Idle;

        return null;
    }
}
