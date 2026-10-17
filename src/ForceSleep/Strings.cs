using System.Globalization;

namespace ForceSleep;

internal enum DeviceKind { Keyboard, Mouse, Network, PowerButton, Lid, Usb, Other }

/// <summary>
/// Every user-visible text, Turkish and English side by side. Turkish is used when Windows'
/// display language is Turkish, English otherwise. Log messages stay English and live in code.
/// </summary>
internal static class L
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");

    public static bool Turkish { get; set; } = WindowsIsTurkish;

    private static bool WindowsIsTurkish => CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "tr"
                                            || CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr";

    /// <summary>Applies the user's choice: "tr", "en", or null to follow Windows.</summary>
    public static void Use(string? language) => Turkish = language switch
    {
        "tr" => true,
        "en" => false,
        _ => WindowsIsTurkish,
    };

    /// <summary>For dates and lower-casing in the UI language.</summary>
    public static CultureInfo Culture => Turkish ? TurkishCulture : EnglishCulture;

    private static string T(string tr, string en) => Turkish ? tr : en;

    // ── Tray ──────────────────────────────────────────────────────────────

    public static string SleepNow => T("Şimdi uyut", "Sleep now");
    public static string Hibernate => T("Hazırda beklet", "Hibernate");
    public static string HibernateUnavailable => T("Bu bilgisayarda hazırda bekletme kapalı.", "Hibernate is turned off on this PC.");
    public static string WhyDidntItSleep => T("Neden uyumadı?", "Why didn't it sleep?");
    public static string HibernateFallback => T("Uyumazsa hazırda beklet", "Hibernate if sleep fails");
    public static string StartWithWindows => T("Windows açılınca başlat", "Start with Windows");
    public static string Exit => T("Çıkış", "Exit");
    // Shown in both languages so someone who can't read the current one can still find it.
    public static string LanguageMenu => T("Dil / Language", "Language / Dil");
    public static string LanguageAuto => T("Otomatik (Windows dili)", "Automatic (Windows language)");
    public static string TooltipIdle => T("uyutmak için tıkla", "click to sleep");
    public static string TooltipBusy => T("uyutuluyor…", "going to sleep…");

    private static string WhereIsIcon => T(
        "Ay simgesi saatin yanında; görmüyorsan ^ okuna tıkla.",
        "The moon icon is next to the clock; if you don't see it, click the ^ arrow.");
    public static string Running => T("Çalışıyor. ", "Running. ") + WhereIsIcon;
    public static string AlreadyRunning => T("Zaten çalışıyor. ", "Already running. ") + WhereIsIcon;
    public static string NoSleepSupport => T(
        "Bu bilgisayarda uyku ve hazırda bekletme kapalı görünüyor.",
        "Sleep and hibernate appear to be turned off on this PC.");
    public static string UnexpectedError => T("Beklenmeyen bir hata oluştu.", "Something unexpected went wrong.");
    public static string AutoStartFailed => T("Otomatik başlatma ayarı değiştirilemedi.", "Couldn't change the start-with-Windows setting.");

    private static string ClickForDetails => T(" Ayrıntılar için tıkla.", " Click for details.");
    public static string ForcedSleep => T("Normal uyku engellendi, bilgisayar zorla uyutuldu.", "Normal sleep was blocked, so the PC was forced to sleep.") + ClickForDetails;
    public static string HibernatedInstead => T("Uyku engellendi, bilgisayar bunun yerine hazırda bekletildi.", "Sleep was blocked, so the PC hibernated instead.") + ClickForDetails;
    public static string WokeAfter(TimeSpan asleep) => T($"Bilgisayar {asleep.TotalSeconds:0} saniye sonra uyandı.", $"The PC woke up after {asleep.TotalSeconds:0} seconds.") + ClickForDetails;
    public static string Cancelled => T("Fare veya klavye hareketi algılandı, uyutma iptal edildi.", "Mouse or keyboard activity detected, so sleep was cancelled.");
    public static string SleepFailed(bool suggestHibernate) => (suggestHibernate
        ? T("Bilgisayar uyutulamadı. Menüden \"Hazırda beklet\"i deneyebilirsin.", "Couldn't put the PC to sleep. You can try \"Hibernate\" from the menu.")
        : T("Bilgisayar uyutulamadı.", "Couldn't put the PC to sleep.")) + ClickForDetails;

    // ── Home ──────────────────────────────────────────────────────────────

    public static string StatusReady => T("Hazır", "Ready");
    public static string StatusReadyWatchdog => T("Bekçi açık: Windows uyumazsa Force Sleep uyutur.", "Watchdog on: if Windows doesn't sleep, Force Sleep will.");
    public static string StatusReadyPlain => T("Bilgisayarı uyutmak için aşağıdaki düğmeye bas.", "Press the button below to put the PC to sleep.");
    public static string StatusSleeping => T("Uyutuluyor…", "Going to sleep…");
    public static string StatusTimer(TimeSpan left) => T($"{Remaining(left)} sonra uyuyacak", $"Sleeping in {Remaining(left)}");
    public static string StatusTimerAt(DateTime at) => T($"Uyuma saati: {at:HH:mm}", $"At {at:HH:mm}");
    public static string StatusKeepAwake => T("Uyanık tutuluyor", "Staying awake");
    public static string StatusKeepAwakeLeft(TimeSpan left) => T($"{Remaining(left)} kaldı", $"{Remaining(left)} left");
    public static string StatusKeepAwakeUntilStopped => T("Sen kapatana kadar", "Until you stop it");
    public static string ScreenOff => T("Ekranı kapat", "Turn off screen");
    public static string SleepTimerTitle => T("Uyku zamanlayıcısı", "Sleep timer");
    public static string KeepAwakeTitle => T("Uyanık tut", "Keep awake");
    public static string Minutes(int n) => T($"{n} dk", $"{n} min");
    public static string Hours(int n) => T($"{n} sa", $"{n} h");
    public static string UntilStopped => T("Süresiz", "No limit");
    public static string CancelTimer => T("Zamanlayıcıyı iptal et", "Cancel timer");
    public static string StopKeepAwake => T("Uyanık tutmayı bitir", "Stop keeping awake");
    public static string HotkeyHint(string hotkey) => T($"Kısayol: {hotkey}", $"Shortcut: {hotkey}");
    public static string Remaining(TimeSpan d) => d.TotalHours >= 1
        ? T($"{(int)d.TotalHours} sa {d.Minutes} dk", $"{(int)d.TotalHours} h {d.Minutes} min")
        : $"{d.Minutes}:{d.Seconds:00}";

    public static string NavHome => T("Ana sayfa", "Home");
    public static string NavSettings => T("Ayarlar", "Settings");
    public static string OpenApp => T("Force Sleep'i aç", "Open Force Sleep");

    // ── Countdown ─────────────────────────────────────────────────────────

    public static string CountdownSleep => T("Bilgisayar uyutuluyor", "Putting the PC to sleep");
    public static string CountdownHibernate => T("Bilgisayar hazırda bekletiliyor", "Hibernating the PC");
    public static string CountdownIn(int seconds) => T($"{seconds} saniye içinde", $"in {seconds} seconds");
    public static string CountdownNow => T("Hemen", "Now");
    public static string Cancel => T("İptal", "Cancel");
    public static string ReasonTimer => T("Uyku zamanlayıcısı doldu.", "The sleep timer is up.");
    public static string ReasonAwayMode => T("Windows uyumak yerine ekranı kapatıp çalışmaya devam etti.", "Windows turned the screen off but kept running instead of sleeping.");
    public static string ReasonWindowsDidNotSleep => T("Windows'un uyuması gereken zaman geçti ama uyumadı.", "Windows was due to sleep but didn't.");
    public static string ReasonIdle(int minutes) => T($"Bilgisayar {minutes} dakikadır kullanılmıyor.", $"The PC hasn't been used for {minutes} minutes.");

    public static string UnexpectedWake(DateTime at, string cause) => T(
        // Time in parentheses: the Turkish suffix ('de/'da/'te/'ta) would depend on the number.
        $"Bilgisayar kendi kendine uyandı ({at:HH:mm}). Uyandıran: {cause}.",
        $"The PC woke up on its own at {at:HH:mm}. Woken by: {cause}.") + ClickForDetails;
    public static string HotkeyInUse => T("Bu kısayol başka bir uygulama tarafından kullanılıyor.", "That shortcut is already used by another app.");

    // ── Settings ──────────────────────────────────────────────────────────

    public static string SettingsAutomatic => T("Otomatik", "Automatic");
    public static string WatchdogTitle => T("Uyku bekçisi", "Sleep watchdog");
    public static string WatchdogDesc => T(
        "Windows uyuması gerekirken uyumazsa (örneğin ekran kapanıp bilgisayar çalışmaya devam ederse) Force Sleep uyutur.",
        "If Windows should sleep but doesn't (for example the screen goes off but the PC keeps running), Force Sleep puts it to sleep.");
    public static string IdleSleepTitle => T("Kullanılmayınca uyut", "Sleep when not in use");
    public static string IdleSleepDesc => T(
        "Bilgisayar seçtiğin süre boyunca kullanılmazsa, uyanık kalmak isteyen programlara rağmen uyutur.",
        "Sleeps after the chosen time without use, even if programs ask to stay awake.");
    public static string WaitForMediaTitle => T("Müzik veya video çalarken bekle", "Wait while music or video plays");
    public static string WaitForMediaDesc => T("Otomatik uyutma, bir şey çalarken yapılmaz.", "Automatic sleep doesn't happen while something is playing.");
    public static string NotifyWakeTitle => T("Beklenmedik uyanmaları bildir", "Tell me about unexpected wake-ups");
    public static string NotifyWakeDesc => T(
        "Bilgisayar sen dokunmadan uyanırsa (zamanlayıcı, ağ kartı…) neyin uyandırdığını gösterir.",
        "If the PC wakes up without you (a timer, the network card…), shows what woke it.");
    public static string SettingsWhenSleeping => T("Uyuturken", "When sleeping");
    public static string CountdownSettingTitle => T("Geri sayım göster", "Show a countdown");
    public static string CountdownSettingDesc => T("Uyutmadan önce 5 saniye bekler; bu sırada iptal edebilirsin.", "Waits 5 seconds before sleeping so you can cancel.");
    public static string PauseMediaTitle => T("Çalan medyayı duraklat", "Pause playing media");
    public static string PauseMediaDesc => T("Uyumadan önce müzik ve videoları duraklatır.", "Pauses music and videos before sleeping.");
    public static string HibernateFallbackDesc => T(
        "Uyku hiçbir şekilde tutmazsa bilgisayarı açık bırakmak yerine hazırda bekletir.",
        "If sleep can't be achieved, hibernates instead of leaving the PC on.");
    public static string ShortcutTitle => T("Klavye kısayolu", "Keyboard shortcut");
    public static string ShortcutDesc => T("Her yerden uyutmak için. Kutuya tıklayıp yeni kısayola bas.", "Sleep from anywhere. Click the box and press a new shortcut.");
    public static string ShortcutNone => T("Yok", "None");
    public static string ShortcutPress => T("Kısayola bas…", "Press a shortcut…");
    public static string Remove => T("Kaldır", "Remove");
    public static string SettingsAppearance => T("Görünüm", "Appearance");
    public static string ThemeTitle => T("Tema", "Theme");
    public static string ThemeSystem => T("Windows ile aynı", "Same as Windows");
    public static string ThemeLight => T("Açık", "Light");
    public static string ThemeDark => T("Koyu", "Dark");
    public static string LanguageTitle => T("Dil", "Language");
    public static string SettingsStartup => T("Başlangıç", "Startup");
    public static string StartWithWindowsDesc => T("Oturum açınca tepside sessizce başlar.", "Starts quietly in the tray when you sign in.");
    public static string SettingsAbout => T("Hakkında", "About");
    public static string VersionText(string version) => T($"Sürüm {version}", $"Version {version}");
    public static string SourceCode => T("GitHub'da kaynak kodu", "Source code on GitHub");
    public static string QuitApp => T("Force Sleep'ten çık", "Quit Force Sleep");
    public static string Yes => T("Evet", "Yes");

    // ── Diagnostics window ────────────────────────────────────────────────

    public static string WindowTitle => T("Neden uyumadı? – Force Sleep", "Why didn't it sleep? – Force Sleep");
    public static string Gathering => T("Bilgiler toplanıyor…", "Gathering information…");
    public static string GatherFailed => T("Bilgiler toplanırken bir hata oluştu.", "Something went wrong while gathering information.");
    public static string DetailedScan => T("Ayrıntılı tara (yönetici izni ister)", "Detailed scan (needs admin)");
    public static string Refresh => T("Yenile", "Refresh");
    public static string LogFile => T("Kayıt dosyası", "Log file");
    public static string Close => T("Kapat", "Close");
    public static string ScanDeclined => T("Yönetici izni verilmediği için ayrıntılı tarama yapılamadı.", "The detailed scan didn't run because admin permission wasn't given.");
    public static string ScanHint => T(
        "Uykuyu engelleyen programları ve uyandırma zamanlayıcılarını görmek için \"Ayrıntılı tara\"ya tıkla.",
        "Click \"Detailed scan\" to see programs blocking sleep and wake timers.");
    public static string Applying => T("Uygulanıyor…", "Applying…");
    public static string ConfirmDisableInputWake => T(
        "Bundan sonra bu aygıtlar bilgisayarı uykudan uyandıramaz. Bilgisayarı uyandırmak için başka bir aygıt veya güç düğmesi gerekir.\n\nDevam edilsin mi?",
        "These devices will no longer wake the PC from sleep. You'll need another device or the power button to wake it.\n\nContinue?");

    // ── Report: summary ───────────────────────────────────────────────────

    public static string SummaryAwayModeRequest(string callers) => T(
        $"\"Uzakta modu\" isteği bulundu: {callers}. Bu, ekranın kapanıp bilgisayarın çalışmaya devam etmesinin tipik sebeplerinden biridir.",
        $"\"Away mode\" request found: {callers}. This is a typical reason for the screen turning off while the PC keeps running.");
    public static string SummaryForced => T("Son denemede normal uyku engellendi; zorla uyku işe yaradı.", "Last time, normal sleep was blocked; forced sleep worked.");
    public static string SummaryHibernated => T("Son denemede uyku tamamen engellendi; bilgisayar hazırda bekletildi.", "Last time, sleep was blocked entirely; the PC hibernated.");
    public static string SummaryFailed => T("Son denemede bilgisayar uyumadı. Aşağıdaki bilgiler sebebi bulmana yardımcı olabilir.", "Last time, the PC didn't sleep. The information below can help find out why.");
    public static string SummaryAwayModeOn => T(
        "\"Uzakta modu\" açık. Bazı programlar bu sayede uykuyu, ekranın kapanıp bilgisayarın çalışmaya devam ettiği bir moda çevirebilir. Aşağıdan kapatabilirsin.",
        "\"Away mode\" is on. It lets some programs turn sleep into a mode where the screen goes off but the PC keeps running. You can turn it off below.");
    public static string SummaryNoSleeps => T("Henüz kayıtlı bir uyku yok.", "No sleep has been recorded yet.");
    public static string SummaryShortSleep(string duration, string? cause) => T(
        $"Bilgisayar son uykudan {duration} sonra uyandı. " + (cause is null ? "Windows uyandıranı kaydetmemiş." : $"Uyandıran: {cause}."),
        $"The PC woke up {duration} after its last sleep. " + (cause is null ? "Windows didn't record what woke it." : $"Woken by: {cause}."));
    public static string SummaryAllGood => T("Son uykularda bir sorun görünmüyor.", "Recent sleeps look fine.");

    // ── Report: sections ──────────────────────────────────────────────────

    public static string LastAttemptTitle => T("Son Force Sleep denemesi", "Last Force Sleep attempt");
    public static string AttemptTime(DateTime at) => T("Zaman: ", "Time: ") + at.ToString(T("d MMMM HH:mm", "MMMM d HH:mm"), Culture);
    public static string AttemptIncomplete => T("Deneme yarıda kalmış (uygulama kapatılmış olabilir).", "The attempt didn't finish (the app may have been closed).");
    public static string Method(SleepMethod method) => method switch
    {
        SleepMethod.Normal => T("Normal uyku", "Normal sleep"),
        SleepMethod.Forced => T("Zorla uyku", "Forced sleep"),
        _ => T("Hazırda bekletme", "Hibernate"),
    };
    public static string Outcome(StepOutcome outcome) => outcome switch
    {
        StepOutcome.Slept => T("işe yaradı", "worked"),
        StepOutcome.Refused => T("Windows reddetti", "Windows refused"),
        StepOutcome.TimedOut => T("bilgisayar uyumadı", "the PC didn't sleep"),
        _ => T("bilgisayar uyumadı, fare veya klavye kullanıldığı için devam edilmedi",
               "the PC didn't sleep; stopped because the mouse or keyboard was used"),
    };

    public static string SleepsTitle => T("Son uykular", "Recent sleeps");
    public static string NoSleepsRecorded => T("Kayıtlı bir uyku bulunamadı.", "No recorded sleeps found.");
    public static string SleptFor(DateTime at, string duration) => at.ToString(T("d MMM HH:mm", "MMM d HH:mm"), Culture) + T($" — {duration} uyudu", $" — slept {duration}");
    public static string WokenBy(string cause) => T($" · Uyandıran: {cause}", $" · Woken by: {cause}");
    public static string Timer(string what) => T($"zamanlayıcı — {what}", $"timer — {what}");
    public static string TimerSleepHint => T(
        "Programlar bilgisayarı zamanlayıcıyla uyandırabilir. İstemiyorsan aşağıdaki \"Windows ayarları\" bölümünden uyandırma zamanlayıcılarını kapatabilirsin.",
        "Programs can wake the PC with a timer. If you don't want that, turn off wake timers in the \"Windows settings\" section below.");

    public static string WakeDevicesTitle => T("Bilgisayarı uyandırabilen aygıtlar", "Devices that can wake the PC");
    public static string NoWakeDevices => T("Hiçbir aygıtın bilgisayarı uyandırmasına izin verilmemiş.", "No device is allowed to wake the PC.");
    public static string DeviceCount(string kind, int count) => T($"{kind} ({count} aygıt)", $"{kind} ({count} devices)");
    public static string NetworkWakeHint => T(
        "Ağ kartının bilgisayarı uyandırmasına izin veriliyor. Ağ üzerinden uyandırma kullanmıyorsan bu, bilgisayarın kendi kendine uyanmasının sık sebeplerinden biridir.",
        "The network card is allowed to wake the PC. If you don't use wake-on-LAN, this is a common reason for the PC waking up by itself.");
    public static string MouseWakeHint => T(
        "Hassas bir fare, masadaki küçük bir titreşimle bile bilgisayarı uyandırabilir.",
        "A sensitive mouse can wake the PC from even a small bump of the desk.");
    public static string DisableWake => T("Uyandırmasını kapat", "Stop it waking the PC");

    public static string SettingsTitle => T("Windows ayarları", "Windows settings");
    public static string AwayModeOn => T("Uzakta modu: açık — programlar uykuyu \"ekran kapalı ama çalışıyor\" moduna çevirebilir",
        "Away mode: on — programs can turn sleep into \"screen off but still running\"");
    public static string AwayModeOff => T("Uzakta modu: kapalı", "Away mode: off");
    public static string WakeTimersOn => T("Uyandırma zamanlayıcıları: açık — programlar bilgisayarı belirli bir saatte uyandırabilir",
        "Wake timers: on — programs can wake the PC at a set time");
    public static string WakeTimersOff => T("Uyandırma zamanlayıcıları: kapalı", "Wake timers: off");
    public static string TurnOff => T("Kapat", "Turn off");

    public static string RequestsTitle => T("Uyanık kalmak isteyen programlar", "Programs asking to stay awake");
    public static string NoRequests => T("Şu an uyanık kalmak isteyen bir program yok.", "No program is asking to stay awake right now.");
    public static string RequestsHint => T(
        "Ekranı açık tutma ve sistem istekleri genelde sadece otomatik uykuyu engeller; elle uyutmaya engel olmaz. \"Uzakta modu\" istekleri ise elle uyutmayı da etkileyebilir. \"Yok say\" dersen Windows o programın bu isteğini artık dikkate almaz.",
        "Display and system requests usually only block automatic sleep, not sleep you start yourself. \"Away mode\" requests can affect both. \"Ignore\" makes Windows disregard that program's request.");
    public static string Ignore => T("Yok say", "Ignore");
    public static string RequestWhat(RequestKind kind) => kind switch
    {
        RequestKind.Display => T("ekranı açık tutmak istiyor", "wants to keep the screen on"),
        RequestKind.System => T("otomatik uykuyu engelliyor", "is blocking automatic sleep"),
        RequestKind.AwayMode => T("uyku yerine \"uzakta modu\" istiyor: ekran kapanır ama bilgisayar çalışmaya devam eder",
                                  "wants \"away mode\" instead of sleep: the screen goes off but the PC keeps running"),
        _ => T("çalışmaya devam etmek istiyor", "wants to keep running"),
    };

    public static string TimersTitle => T("Uyandırma zamanlayıcıları", "Wake timers");
    public static string NoTimers => T("Bilgisayarı ileride uyandırmak için kurulmuş bir zamanlayıcı yok.", "No timer is set to wake the PC.");

    public static string ChangesTitle => T("Force Sleep ile yaptığın değişiklikler", "Changes you made with Force Sleep");
    public static string Undo => T("Geri al", "Undo");
    public static string CannotWake(string device) => T($"{device} bilgisayarı uyandıramaz", $"{device} can't wake the PC");
    public static string RequestIgnored(string source, string requests) => T(
        $"{source} uykuyu engelleyemez (yok sayılan: {requests})", $"{source} can't block sleep (ignored: {requests})");
    public static string SettingTurnedOff(string setting) => T($"{setting} kapatıldı", $"{setting} turned off");

    public static string Duration(TimeSpan d) => d switch
    {
        { TotalMinutes: < 1 } => T($"{Math.Max(1, (int)d.TotalSeconds)} saniye", $"{Math.Max(1, (int)d.TotalSeconds)} seconds"),
        { TotalHours: < 1 } => T($"{(int)d.TotalMinutes} dakika", $"{(int)d.TotalMinutes} minutes"),
        { TotalDays: < 2 } => T($"{(int)d.TotalHours} saat {d.Minutes} dakika", $"{(int)d.TotalHours} h {d.Minutes} min"),
        _ => T($"{d.Days} gün {d.Hours} saat", $"{d.Days} days {d.Hours} h"),
    };

    // ── Fixes ─────────────────────────────────────────────────────────────

    public static string FixDevicesDone => T("Tamam, bu aygıtlar artık bilgisayarı uyandıramaz.", "Done, these devices can no longer wake the PC.");
    public static string FixRequestDone(string source) => T($"Tamam, {source} artık bu şekilde uykuyu engelleyemez.", $"Done, {source} can no longer block sleep this way.");
    public static string FixSettingDone(string setting) => T($"Tamam, {setting.ToLower(Culture)} kapatıldı.", $"Done, {setting.ToLower(Culture)} turned off.");
    public static string SettingReadFailed => T("Ayar okunamadı.", "Couldn't read the setting.");
    public static string SettingWriteFailed => T("Ayar değiştirilemedi.", "Couldn't change the setting.");
    public static string UndoFailed => T("Ayar geri alınamadı.", "Couldn't undo the setting.");
    public static string UndoDone => T("Değişiklik geri alındı.", "Change undone.");
    public static string AdminDeclined => T("Yönetici izni verilmediği için değişiklik yapılmadı.", "Nothing changed because admin permission wasn't given.");
    public static string FixFailed => T("Değişiklik yapılamadı. Ayrıntılar kayıt dosyasında.", "The change failed. See the log file for details.");
    public static string SettingName(PowerSetting setting) => setting switch
    {
        PowerSetting.AwayMode => T("Uzakta modu", "Away mode"),
        _ => T("Uyandırma zamanlayıcıları", "Wake timers"),
    };

    // ── Names of things ───────────────────────────────────────────────────

    public static string Device(DeviceKind kind) => kind switch
    {
        DeviceKind.Keyboard => T("Klavye", "Keyboard"),
        DeviceKind.Mouse => T("Fare", "Mouse"),
        DeviceKind.Network => T("Ağ kartı", "Network card"),
        DeviceKind.PowerButton => T("Güç düğmesi", "Power button"),
        DeviceKind.Lid => T("Kapak", "Lid"),
        DeviceKind.Usb => T("USB aygıtı", "USB device"),
        _ => "",
    };
    public static string Service(string name) => T($"Windows hizmeti: {name}", $"Windows service: {name}");
    public static string Driver(string name) => T($"Sürücü: {name}", $"Driver: {name}");
    public static string ScheduledTask(string name, bool windowsUpdate) =>
        T($"Zamanlanmış görev: {name}", $"Scheduled task: {name}") + (windowsUpdate ? " (Windows Update)" : "");
}
