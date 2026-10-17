namespace ForceSleep.Tests;

public class SleepPolicyTests
{
    private static readonly AutoSleepSettings Defaults = new(Watchdog: true, IdleSleep: false, TimeSpan.FromMinutes(30), WaitForMedia: true);

    /// <summary>A quiet machine, an hour after resuming, that has never tried to sleep on its own.</summary>
    private static PolicyInput Input(
        AutoSleepSettings? settings = null,
        double idleMinutes = 0,
        bool displayOff = false,
        bool awayMode = false,
        double? windowsSleepMinutes = 30,
        bool media = false,
        bool keepAwake = false,
        double sinceResumeMinutes = 60,
        double sinceAttemptMinutes = 60) => new(
            settings ?? Defaults,
            TimeSpan.FromMinutes(idleMinutes),
            displayOff,
            awayMode,
            windowsSleepMinutes is { } w ? TimeSpan.FromMinutes(w) : null,
            media,
            keepAwake,
            TimeSpan.FromMinutes(sinceResumeMinutes),
            TimeSpan.FromMinutes(sinceAttemptMinutes));

    [Fact]
    public void AwayMode_SleepsSoonAfterTheUserWalksAway()
    {
        Assert.Null(SleepPolicy.Decide(Input(awayMode: true, idleMinutes: 0.2)));
        Assert.Equal(AutoSleepReason.AwayMode, SleepPolicy.Decide(Input(awayMode: true, idleMinutes: 1)));
    }

    [Fact]
    public void AwayMode_ActsEvenRightAfterResume()
    {
        // Sleep pressed shortly after waking still deserves to work.
        Assert.Equal(AutoSleepReason.AwayMode, SleepPolicy.Decide(Input(awayMode: true, idleMinutes: 1, sinceResumeMinutes: 1)));
    }

    [Fact]
    public void WindowsDidNotSleep_OnlyWithScreenOffAndPastWindowsTimeoutPlusGrace()
    {
        Assert.Null(SleepPolicy.Decide(Input(displayOff: true, idleMinutes: 31)));
        Assert.Null(SleepPolicy.Decide(Input(displayOff: false, idleMinutes: 45)));
        Assert.Equal(AutoSleepReason.WindowsDidNotSleep, SleepPolicy.Decide(Input(displayOff: true, idleMinutes: 33)));
    }

    [Fact]
    public void WindowsSetToNeverSleep_IsRespected()
    {
        Assert.Null(SleepPolicy.Decide(Input(displayOff: true, idleMinutes: 600, windowsSleepMinutes: null)));
    }

    [Fact]
    public void IdleSleep_UsesItsOwnTimeRegardlessOfScreen()
    {
        var settings = Defaults with { Watchdog = false, IdleSleep = true, IdleSleepAfter = TimeSpan.FromMinutes(20) };
        Assert.Null(SleepPolicy.Decide(Input(settings, idleMinutes: 19)));
        Assert.Equal(AutoSleepReason.Idle, SleepPolicy.Decide(Input(settings, idleMinutes: 21)));
    }

    [Fact]
    public void PlayingMedia_PostponesEverything_UnlessTheUserDoesNotWantThat()
    {
        Assert.Null(SleepPolicy.Decide(Input(awayMode: true, idleMinutes: 5, media: true)));
        Assert.Null(SleepPolicy.Decide(Input(displayOff: true, idleMinutes: 40, media: true)));

        var noWaiting = Defaults with { WaitForMedia = false };
        Assert.Equal(AutoSleepReason.WindowsDidNotSleep,
            SleepPolicy.Decide(Input(noWaiting, displayOff: true, idleMinutes: 40, media: true)));
    }

    [Fact]
    public void KeepAwake_Wins()
    {
        Assert.Null(SleepPolicy.Decide(Input(awayMode: true, displayOff: true, idleMinutes: 600, keepAwake: true)));
    }

    [Fact]
    public void RecentResumeOrAttempt_HoldsOff()
    {
        // Woken by a timer: give whatever woke it time to work.
        Assert.Null(SleepPolicy.Decide(Input(displayOff: true, idleMinutes: 600, sinceResumeMinutes: 2)));
        // Just tried: don't loop.
        Assert.Null(SleepPolicy.Decide(Input(awayMode: true, idleMinutes: 5, sinceAttemptMinutes: 3)));
    }

    [Fact]
    public void Watchdog_Off_IgnoresAwayModeAndWindowsTimeout()
    {
        var off = Defaults with { Watchdog = false };
        Assert.Null(SleepPolicy.Decide(Input(off, awayMode: true, displayOff: true, idleMinutes: 600)));
    }
}
