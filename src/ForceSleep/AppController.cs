using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;

namespace ForceSleep;

/// <summary>What started a sleep, which decides the countdown text and how the result is reported.</summary>
internal abstract record SleepTrigger
{
    public sealed record Manual(bool Hibernate = false) : SleepTrigger;
    public sealed record Timer : SleepTrigger;
    public sealed record Auto(AutoSleepReason Reason) : SleepTrigger;
}

/// <summary>What the controller needs from the UI, so the logic doesn't depend on windows.</summary>
internal interface IUserInterface
{
    /// <summary>Shows the countdown; true to go ahead, false if the user cancelled.</summary>
    Task<bool> CountdownAsync(string title, string? reason, TimeSpan duration);

    void Notify(string message, bool warning = false, bool opensDiagnostics = false);
}

/// <summary>
/// The app's brain: sleeping, the sleep timer, keep-awake, the watchdog and idle sleep, the
/// hotkey and wake notifications. Lives on the UI thread; views bind to its properties.
/// </summary>
internal sealed class AppController : INotifyPropertyChanged, IDisposable
{
    public static readonly TimeSpan CountdownLength = TimeSpan.FromSeconds(5);

    // Waking this soon after a sleep was most likely not intended, so say so.
    private static readonly TimeSpan ShortSleepNotice = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PolicyInterval = TimeSpan.FromSeconds(10);

    private readonly IUserInterface _ui;
    private readonly SleepController _sleep = new(PowerCapabilities.Query());
    private readonly KeepAwake _keepAwake = new();
    private readonly SystemEvents _events = new();
    private readonly DispatcherTimer _clock;
    private DateTime _lastResume = DateTime.Now;
    private DateTime _lastAutoAttempt = DateTime.MinValue;
    private DateTime _lastPolicyCheck = DateTime.MinValue;
    private bool _checkingPolicy;
    private bool _busy;
    private DateTime? _sleepTimerEnd;

    public AppController(Settings settings, IUserInterface ui)
    {
        Settings = settings;
        _ui = ui;

        _events.HotkeyPressed += () => _ = SleepAsync(new SleepTrigger.Manual());
        _events.Resumed += OnResumed;
        _events.AwayModeChanged += _ => _lastPolicyCheck = DateTime.MinValue;
        ApplyHotkey();

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => OnTick();
        _clock.Start();

        Log.Write($"Started {Program.Version}. {Capabilities}");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Settings Settings { get; }

    public PowerCapabilities Capabilities => _sleep.Capabilities;

    public bool CanSleep => _sleep.CanDoAnything;

    public bool Busy
    {
        get => _busy;
        private set => Set(ref _busy, value);
    }

    public DateTime? SleepTimerEnd
    {
        get => _sleepTimerEnd;
        private set => Set(ref _sleepTimerEnd, value);
    }

    public bool NotBusy => !Busy;

    public bool SleepTimerActive => SleepTimerEnd is not null;

    public bool KeepAwakeActive => _keepAwake.IsActive;

    public string HotkeyHint => Hotkey.Parse(Settings.Hotkey) is { } hotkey ? L.HotkeyHint(hotkey.ToString()) : "";

    public string StatusTitle =>
        Busy ? L.StatusSleeping
        : SleepTimerEnd is { } end ? L.StatusTimer(Remaining(end))
        : KeepAwakeActive ? L.StatusKeepAwake
        : L.StatusReady;

    public string StatusDetail =>
        Busy ? ""
        : SleepTimerEnd is { } end ? L.StatusTimerAt(end)
        : KeepAwakeActive ? (_keepAwake.Until is { } until ? L.StatusKeepAwakeLeft(Remaining(until)) : L.StatusKeepAwakeUntilStopped)
        : Settings.Watchdog ? L.StatusReadyWatchdog
        : L.StatusReadyPlain;

    // ── Actions ───────────────────────────────────────────────────────────

    public async Task SleepAsync(SleepTrigger trigger)
    {
        if (Busy || !CanSleep)
            return;

        var hibernate = trigger is SleepTrigger.Manual { Hibernate: true };
        Busy = true;
        try
        {
            if (Settings.Countdown)
            {
                var title = hibernate ? L.CountdownHibernate : L.CountdownSleep;
                if (!await _ui.CountdownAsync(title, ReasonText(trigger), CountdownLength))
                {
                    Log.Write($"Sleep cancelled during countdown ({trigger})");
                    return;
                }
            }

            // A deliberate sleep ends whatever was keeping the machine awake.
            SleepTimerEnd = null;
            StopKeepAwake();

            if (Settings.PauseMediaBeforeSleep)
                await MediaControl.PauseAllAsync();

            var fallback = Settings.HibernateFallback && Capabilities.CanHibernate;
            var result = hibernate ? await _sleep.HibernateAsync() : await _sleep.SleepAsync(fallback);
            Report(result, hibernate);
        }
        catch (Exception e)
        {
            Log.Write($"Unexpected error: {e}");
            _ui.Notify(L.UnexpectedError, warning: true);
        }
        finally
        {
            Busy = false;
        }
    }

    public void TurnScreenOff()
    {
        // Give the click that triggered this a moment, or the release itself turns the screen back on.
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Native.TurnScreenOff();
        };
        timer.Start();
    }

    public void StartSleepTimer(TimeSpan duration)
    {
        StopKeepAwake();
        SleepTimerEnd = DateTime.Now + duration;
        Log.Write($"Sleep timer set for {duration.TotalMinutes:0} min");
    }

    public void CancelSleepTimer()
    {
        SleepTimerEnd = null;
        Log.Write("Sleep timer cancelled");
    }

    public void StartKeepAwake(TimeSpan? duration)
    {
        SleepTimerEnd = null;
        _keepAwake.Start(duration);
        Changed(nameof(KeepAwakeActive));
    }

    public void StopKeepAwake()
    {
        _keepAwake.Stop();
        Changed(nameof(KeepAwakeActive));
    }

    /// <summary>Registers the shortcut from the settings; false if another app owns it.</summary>
    public bool ApplyHotkey() => _events.SetHotkey(Hotkey.Parse(Settings.Hotkey));

    /// <summary>Settings changed elsewhere (the settings page); refresh what depends on them.</summary>
    public void SettingsChanged()
    {
        Settings.Save();
        Changed(nameof(StatusDetail));
        Changed(nameof(HotkeyHint));
        _lastPolicyCheck = DateTime.MinValue;
    }

    // ── Background work ───────────────────────────────────────────────────

    private void OnTick()
    {
        var now = DateTime.Now;

        if (SleepTimerEnd is { } end && now >= end && !Busy)
        {
            SleepTimerEnd = null;
            _ = SleepAsync(new SleepTrigger.Timer());
        }

        if (_keepAwake.StopIfExpired(now))
            Changed(nameof(KeepAwakeActive));

        Changed(nameof(StatusTitle));
        Changed(nameof(StatusDetail));

        if (now - _lastPolicyCheck >= PolicyInterval)
        {
            _lastPolicyCheck = now;
            _ = CheckPolicyAsync(now);
        }
    }

    private async Task CheckPolicyAsync(DateTime now)
    {
        if (Busy || _checkingPolicy)
            return;

        _checkingPolicy = true;
        try
        {
            var input = new PolicyInput(
                Settings.AutoSleep,
                Native.IdleTime(),
                _events.DisplayOff,
                _events.AwayMode,
                PowerSettings.WindowsSleepTimeout(),
                MediaPlaying: false,
                _keepAwake.IsActive,
                now - _lastResume,
                now - _lastAutoAttempt);

            // Asking the media controls is the slow part, so only do it when it could matter.
            if (SleepPolicy.Decide(input) is null)
                return;
            if (Settings.WaitForMedia && await MediaControl.IsAnythingPlayingAsync())
                input = input with { MediaPlaying = true };
            if (SleepPolicy.Decide(input) is not { } reason)
                return;

            _lastAutoAttempt = DateTime.Now;
            Log.Write($"Automatic sleep: {reason} (idle {input.Idle.TotalMinutes:0} min)");
            await SleepAsync(new SleepTrigger.Auto(reason));
        }
        finally
        {
            _checkingPolicy = false;
        }
    }

    private async void OnResumed()
    {
        _lastResume = DateTime.Now;
        if (!Settings.NotifyUnexpectedWake)
            return;

        // Windows writes the wake event a few seconds after resuming.
        await Task.Delay(TimeSpan.FromSeconds(15));
        if (SleepHistory.Recent(1) is [var last]
            && DateTime.Now - last.WokeAt < TimeSpan.FromMinutes(5)
            && UnexpectedWakeCause(last) is { } cause)
        {
            _ui.Notify(L.UnexpectedWake(last.WokeAt, cause), opensDiagnostics: true);
        }
    }

    /// <summary>
    /// The cause if the machine woke without the user: a timer or a device that isn't something
    /// a person presses. Unknown causes stay quiet rather than risk a false alarm.
    /// </summary>
    public static string? UnexpectedWakeCause(SleepRecord wake)
    {
        if (wake.WakeTimerOwner is not null)
            return DiagnosticsReport.WakeCause(wake);
        if (wake.WakeSource is { } source && PowerCfg.ClassifyDevice(source) == DeviceKind.Network)
            return DiagnosticsReport.WakeCause(wake);
        return null;
    }

    private void Report(SleepResult result, bool requestedHibernate)
    {
        switch (result)
        {
            case { Status: SleepStatus.Slept, Method: SleepMethod.Forced }:
                _ui.Notify(L.ForcedSleep, opensDiagnostics: true);
                break;
            case { Status: SleepStatus.Slept, Method: SleepMethod.Hibernate } when !requestedHibernate:
                _ui.Notify(L.HibernatedInstead, opensDiagnostics: true);
                break;
            case { Status: SleepStatus.Slept } when result.TimeAsleep < ShortSleepNotice:
                _ui.Notify(L.WokeAfter(result.TimeAsleep), opensDiagnostics: true);
                break;
            case { Status: SleepStatus.Cancelled }:
                _ui.Notify(L.Cancelled);
                break;
            case { Status: SleepStatus.Failed }:
                _ui.Notify(L.SleepFailed(suggestHibernate: Capabilities.CanHibernate && !Settings.HibernateFallback),
                    warning: true, opensDiagnostics: true);
                break;
        }
    }

    private string? ReasonText(SleepTrigger trigger) => trigger switch
    {
        SleepTrigger.Timer => L.ReasonTimer,
        SleepTrigger.Auto { Reason: AutoSleepReason.AwayMode } => L.ReasonAwayMode,
        SleepTrigger.Auto { Reason: AutoSleepReason.WindowsDidNotSleep } => L.ReasonWindowsDidNotSleep,
        SleepTrigger.Auto { Reason: AutoSleepReason.Idle } => L.ReasonIdle(Settings.IdleSleepMinutes),
        _ => null,
    };

    private static TimeSpan Remaining(DateTime until)
    {
        var left = until - DateTime.Now;
        return left < TimeSpan.Zero ? TimeSpan.Zero : left;
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        Changed(name!);
        Changed(nameof(NotBusy));
        Changed(nameof(SleepTimerActive));
        Changed(nameof(StatusTitle));
        Changed(nameof(StatusDetail));
    }

    private void Changed(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        _clock.Stop();
        _keepAwake.Stop();
        _events.Dispose();
    }
}
