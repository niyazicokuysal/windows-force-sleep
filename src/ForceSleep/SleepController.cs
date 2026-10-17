using System.Runtime.InteropServices;

namespace ForceSleep;

internal enum SleepMethod
{
    /// <summary>Regular sleep, same as Start &gt; Sleep.</summary>
    Normal,
    /// <summary>Critical sleep: apps and drivers are not asked, so they cannot veto it.</summary>
    Forced,
    Hibernate,
}

internal enum SleepStatus { Slept, Cancelled, Failed }

/// <summary>How a single method attempt ended. Written to the log and read back by <see cref="AttemptHistory"/>.</summary>
internal enum StepOutcome { Slept, Refused, TimedOut, UserReturned }

internal sealed record SleepResult(SleepStatus Status, SleepMethod? Method = null, TimeSpan TimeAsleep = default);

/// <summary>
/// Tries progressively stronger ways to put the machine to sleep and verifies each one
/// actually happened before giving up on it.
/// </summary>
internal sealed class SleepController(PowerCapabilities caps)
{
    /// <summary>Log line that starts each attempt, so the log can be split into attempts.</summary>
    public const string AttemptMarker = "Sleep requested";

    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    // Anything shorter is clock jitter, not a real sleep.
    private static readonly TimeSpan MinTimeAsleep = TimeSpan.FromSeconds(1);

    private readonly record struct StepResult(StepOutcome Outcome, TimeSpan TimeAsleep = default);

    public PowerCapabilities Capabilities => caps;

    public bool CanDoAnything => caps.CanSleep || caps.CanHibernate || caps.ModernStandby;

    public Task<SleepResult> SleepAsync(bool hibernateFallback) => RunAsync(Plan(hibernateFallback));

    public Task<SleepResult> HibernateAsync() => RunAsync([SleepMethod.Hibernate]);

    private async Task<SleepResult> RunAsync(IEnumerable<SleepMethod> plan)
    {
        Log.Write(AttemptMarker);

        foreach (var method in plan)
        {
            var step = await TryAsync(method);
            Log.Write(step.Outcome == StepOutcome.Slept
                ? $"{method}: {step.Outcome} ({step.TimeAsleep.TotalSeconds:0} s)"
                : $"{method}: {step.Outcome}");

            switch (step.Outcome)
            {
                case StepOutcome.Slept:
                    return new SleepResult(SleepStatus.Slept, method, step.TimeAsleep);
                case StepOutcome.UserReturned:
                    return new SleepResult(SleepStatus.Cancelled);
            }
        }

        return new SleepResult(SleepStatus.Failed);
    }

    private IEnumerable<SleepMethod> Plan(bool hibernateFallback)
    {
        if (caps.ModernStandby)
        {
            // Modern Standby machines have no classic sleep to force; their "sleep" is a
            // screen-off connected state. Hibernate is the only real power-down.
            yield return caps.CanHibernate ? SleepMethod.Hibernate : SleepMethod.Normal;
            yield break;
        }

        if (caps.CanSleep)
        {
            yield return SleepMethod.Normal;
            yield return SleepMethod.Forced;
        }

        if (caps.CanHibernate && (hibernateFallback || !caps.CanSleep))
            yield return SleepMethod.Hibernate;
    }

    private async Task<StepResult> TryAsync(SleepMethod method)
    {
        var clock = SleepClock.Now();
        var startedAt = DateTime.Now;
        var inputAtStart = Native.LastInputTick();

        // The sleep APIs block until the machine resumes, so call them off the UI thread
        // and watch the clocks from here instead.
        var call = Task.Run(() => Invoke(method));

        // Modern Standby does not stop the unbiased clock, so it cannot be verified.
        if (method == SleepMethod.Normal && caps.ModernStandby)
            return new StepResult(StepOutcome.Slept);

        // Input does not end the step: Windows can take seconds to prepare (hibernate writes
        // memory to disk first) and a nudged mouse in that window must not count as a cancel.
        // It only stops us from escalating to a harsher method afterwards.
        var userActive = false;
        for (var waited = TimeSpan.Zero; waited < StepTimeout; waited += PollInterval)
        {
            await Task.Delay(PollInterval);

            var asleep = clock.TimeAsleepSince();
            if (asleep >= MinTimeAsleep)
                return new StepResult(StepOutcome.Slept, asleep);
            if (call.IsCompleted && !call.Result)
                return new StepResult(StepOutcome.Refused);
            userActive |= Native.LastInputTick() != inputAtStart;
        }

        return await ConfirmWithEventLogAsync(startedAt, userActive ? StepOutcome.UserReturned : StepOutcome.TimedOut);
    }

    /// <summary>
    /// Last check before concluding the machine stayed awake: Windows writes a wake event after
    /// every sleep and hibernate, sometimes a few seconds after resume.
    /// </summary>
    private static async Task<StepResult> ConfirmWithEventLogAsync(DateTime startedAt, StepOutcome otherwise)
    {
        for (var i = 0; i < 5; i++)
        {
            if (SleepHistory.Recent(1) is [var last] && last.SleptAt >= startedAt.AddSeconds(-1))
                return new StepResult(StepOutcome.Slept, last.Duration);
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        return new StepResult(otherwise);
    }

    private static bool Invoke(SleepMethod method)
    {
        switch (method)
        {
            case SleepMethod.Normal:
            case SleepMethod.Hibernate:
                if (Native.SetSuspendState(hibernate: method == SleepMethod.Hibernate, false, false))
                    return true;
                Log.Write($"SetSuspendState failed, error {Marshal.GetLastPInvokeError()}");
                return false;

            case SleepMethod.Forced:
                var status = Native.NtInitiatePowerAction(
                    Native.PowerActionSleep,
                    Native.PowerSystemSleeping1,
                    Native.PowerActionCritical | Native.PowerActionOverrideApps,
                    asynchronous: false);
                if (status >= 0)
                    return true;
                Log.Write($"NtInitiatePowerAction failed, status 0x{status:X8} (error {Native.RtlNtStatusToDosError(status)})");
                return false;

            default:
                throw new ArgumentOutOfRangeException(nameof(method));
        }
    }
}
