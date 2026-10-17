namespace ForceSleep;

/// <summary>
/// Detects whether the machine actually slept. GetTickCount64 keeps counting during
/// sleep/hibernation while the unbiased interrupt time does not, so the gap between
/// the two is exactly the time spent asleep.
/// </summary>
internal readonly struct SleepClock
{
    private readonly long _tickMs;
    private readonly ulong _unbiased100ns;

    private SleepClock(long tickMs, ulong unbiased100ns)
    {
        _tickMs = tickMs;
        _unbiased100ns = unbiased100ns;
    }

    public static SleepClock Now()
    {
        Native.QueryUnbiasedInterruptTime(out var unbiased);
        return new SleepClock(Environment.TickCount64, unbiased);
    }

    public TimeSpan TimeAsleepSince()
    {
        var now = Now();
        var wallMs = now._tickMs - _tickMs;
        var awakeMs = (long)((now._unbiased100ns - _unbiased100ns) / 10_000);
        return TimeSpan.FromMilliseconds(Math.Max(0, wallMs - awakeMs));
    }
}
