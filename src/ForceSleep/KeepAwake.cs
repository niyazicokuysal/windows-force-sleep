namespace ForceSleep;

/// <summary>
/// Keeps the machine from sleeping on its own (the screen may still turn off). Must be used
/// from one thread, because Windows ties the request to the calling thread.
/// </summary>
internal sealed class KeepAwake
{
    public bool IsActive { get; private set; }

    /// <summary>When it ends by itself, or null if it lasts until stopped.</summary>
    public DateTime? Until { get; private set; }

    public void Start(TimeSpan? duration)
    {
        Native.SetThreadExecutionState(Native.ES_CONTINUOUS | Native.ES_SYSTEM_REQUIRED);
        IsActive = true;
        Until = duration is { } d ? DateTime.Now + d : null;
        Log.Write($"Keep awake started ({(duration is { } x ? $"{x.TotalMinutes:0} min" : "until stopped")})");
    }

    public void Stop()
    {
        if (!IsActive)
            return;
        Native.SetThreadExecutionState(Native.ES_CONTINUOUS);
        IsActive = false;
        Until = null;
        Log.Write("Keep awake stopped");
    }

    /// <summary>Stops it if its time is up; returns true if it did.</summary>
    public bool StopIfExpired(DateTime now)
    {
        if (!IsActive || Until is not { } until || now < until)
            return false;
        Stop();
        return true;
    }
}
