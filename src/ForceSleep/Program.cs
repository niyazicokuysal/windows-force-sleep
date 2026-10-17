namespace ForceSleep;

internal static class Program
{
    private const string TrayMutexName = @"Local\ForceSleep.Tray";
    private const string ActivateEventName = @"Local\ForceSleep.Activate";
    private const string QuitEventName = @"Local\ForceSleep.Quit";

    public static string Version { get; } = typeof(Program).Assembly.GetName().Version is { } v
        ? $"{v.Major}.{v.Minor}.{v.Build}"
        : "?";

    [STAThread]
    private static int Main(string[] args)
    {
        // powercfg writes in the console's OEM code page, which .NET does not ship by default.
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        if (!Native.EnableShutdownPrivilege())
            Log.Write("Could not enable SeShutdownPrivilege; sleep calls may fail.");

        // Started by PowerCfg.RunElevatedAsync behind a UAC prompt.
        if (args is [PowerCfg.ElevatedArgument, var payload])
            return PowerCfg.RunAsElevatedChild(payload);

        var settings = Settings.Load();
        L.Use(settings.Language);

        // Used by the installer to carry over the language picked in setup, only on first install
        // so an update never overrides a choice made later in the app.
        if (args is ["--default-language", var language] && language is "tr" or "en")
        {
            if (settings.Language is null)
            {
                settings.Language = language;
                settings.Save();
            }
            return 0;
        }

        // Used by the installer: close the running instance before files are replaced or removed.
        if (Has(args, "--quit"))
        {
            QuitRunningInstance();
            return 0;
        }

        if (Has(args, "--uninstall"))
        {
            QuitRunningInstance();
            AutoStart.Set(false);
            return 0;
        }

        if (Has(args, "--diagnose"))
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(DiagnosticsReport.Build(DiagnosticsReport.Gather(scan: null, settings)));
            return 0;
        }

        // Diagnostics: prints what this machine supports (stdout works when redirected).
        if (Has(args, "--status"))
        {
            var caps = PowerCapabilities.Query();
            Console.WriteLine($"Force Sleep {Version}");
            Console.WriteLine(caps);
            Console.WriteLine($"CanSleep={caps.CanSleep} CanHibernate={caps.CanHibernate}");
            Console.WriteLine($"WindowsSleepTimeout={PowerSettings.WindowsSleepTimeout()?.ToString() ?? "never"} " +
                              $"Idle={Native.IdleTime():hh\\:mm\\:ss} MediaPlaying={MediaControl.IsAnythingPlayingAsync().GetAwaiter().GetResult()}");
            return 0;
        }

        // Headless mode for shortcuts and scripts: sleep once, then exit.
        if (Has(args, "--sleep"))
        {
            var controller = new SleepController(PowerCapabilities.Query());
            var result = controller.SleepAsync(settings.HibernateFallback).GetAwaiter().GetResult();
            return result.Status == SleepStatus.Slept ? 0 : 1;
        }

        using var mutex = new Mutex(initiallyOwned: true, TrayMutexName, out var isFirstInstance);
        using var activate = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        if (!isFirstInstance)
        {
            // Bring up the running copy's window instead of silently exiting.
            activate.Set();
            return 0;
        }

        // Started by Windows at sign-in: stay in the tray. Started by the user: show the window.
        Ui.Page? startPage =
            Has(args, AutoStart.Argument) ? null
            : Has(args, "--why") ? Ui.Page.Why
            : Has(args, "--settings") ? Ui.Page.Settings
            : Ui.Page.Home;

        using var quit = new EventWaitHandle(false, EventResetMode.ManualReset, QuitEventName);
        new Ui.App(settings, activate, quit, startPage).Run();
        return 0;
    }

    private static bool Has(string[] args, string flag) => args.Contains(flag, StringComparer.OrdinalIgnoreCase);

    /// <summary>Asks the tray instance to exit and waits until it has.</summary>
    private static void QuitRunningInstance()
    {
        if (!EventWaitHandle.TryOpenExisting(QuitEventName, out var quit))
            return;

        using (quit)
            quit.Set();

        if (!Mutex.TryOpenExisting(TrayMutexName, out var mutex))
            return;

        using (mutex)
        {
            try
            {
                if (mutex.WaitOne(TimeSpan.FromSeconds(10)))
                    mutex.ReleaseMutex();
            }
            catch (AbandonedMutexException)
            {
                // The instance exited without releasing it, which is just as good.
            }
        }
    }
}
