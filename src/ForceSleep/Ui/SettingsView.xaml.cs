using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ForceSleep.Ui;

internal partial class SettingsView : UserControl
{
    private const string RepoUrl = "https://github.com/niyazicokuysal/windows-force-sleep";
    private static readonly int[] IdleChoices = [15, 30, 45, 60, 90, 120];
    private static readonly string?[] Themes = [null, "light", "dark"];
    private static readonly string?[] Languages = [null, "tr", "en"];

    private readonly AppController _controller;
    private readonly Settings _settings;
    private bool _loading = true;

    public SettingsView(AppController controller)
    {
        _controller = controller;
        _settings = controller.Settings;
        InitializeComponent();
        // Toggles bind straight to the settings object; OnChanged saves after each click.
        DataContext = _settings;

        HibernateFallback.IsEnabled = controller.Capabilities.CanHibernate;

        IdleMinutes.ItemsSource = IdleChoices
            .Select(m => m < 60 ? L.Minutes(m) : m % 60 == 0 ? L.Hours(m / 60) : L.Remaining(TimeSpan.FromMinutes(m)))
            .ToList();
        IdleMinutes.SelectedIndex = Math.Max(0, Array.IndexOf(IdleChoices, _settings.IdleSleepMinutes));

        ThemeBox.ItemsSource = new[] { L.ThemeSystem, L.ThemeLight, L.ThemeDark };
        ThemeBox.SelectedIndex = Math.Max(0, Array.IndexOf(Themes, _settings.Theme));

        LanguageBox.ItemsSource = new[] { L.LanguageAuto, "Türkçe", "English" };
        LanguageBox.SelectedIndex = Math.Max(0, Array.IndexOf(Languages, _settings.Language));

        AutoStartBox.IsChecked = AutoStart.IsEnabled;
        VersionText.Text = L.VersionText(Program.Version);
        ShowHotkey();
        _loading = false;
    }

    private void OnChanged(object sender, RoutedEventArgs e) => _controller.SettingsChanged();

    private void OnIdleMinutesChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || IdleMinutes.SelectedIndex < 0)
            return;
        _settings.IdleSleepMinutes = IdleChoices[IdleMinutes.SelectedIndex];
        _controller.SettingsChanged();
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        _settings.Theme = Themes[ThemeBox.SelectedIndex];
        _controller.SettingsChanged();
        App.Current.ApplyTheme();
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;
        // Rebuilds this window, so defer until the selection change has finished.
        var language = Languages[LanguageBox.SelectedIndex];
        Dispatcher.BeginInvoke(() => App.Current.SetLanguage(language));
    }

    private void OnAutoStart(object sender, RoutedEventArgs e)
    {
        try
        {
            AutoStart.Set(AutoStartBox.IsChecked == true);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log.Write($"Could not change auto start: {ex.Message}");
        }
        AutoStartBox.IsChecked = AutoStart.IsEnabled;
    }

    // ── Shortcut ──────────────────────────────────────────────────────────

    private void OnHotkeyFocus(object sender, KeyboardFocusChangedEventArgs e) => HotkeyBox.Text = L.ShortcutPress;

    private void OnHotkeyBlur(object sender, KeyboardFocusChangedEventArgs e) => ShowHotkey();

    private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        // With Alt held, WPF reports the real key in SystemKey.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Escape or Key.Tab)
        {
            Keyboard.ClearFocus();
            return;
        }
        if (Hotkey.Create(Keyboard.Modifiers, key) is not { } hotkey)
            return;

        var previous = _settings.Hotkey;
        _settings.Hotkey = hotkey.ToString();
        if (_controller.ApplyHotkey())
        {
            HotkeyError.Visibility = Visibility.Collapsed;
            _controller.SettingsChanged();
        }
        else
        {
            // Taken by another app: keep the old one working.
            HotkeyError.Visibility = Visibility.Visible;
            _settings.Hotkey = previous;
            _controller.ApplyHotkey();
        }
        Keyboard.ClearFocus();
        ShowHotkey();
    }

    private void OnRemoveHotkey(object sender, RoutedEventArgs e)
    {
        _settings.Hotkey = "";
        _controller.ApplyHotkey();
        _controller.SettingsChanged();
        HotkeyError.Visibility = Visibility.Collapsed;
        ShowHotkey();
    }

    private void ShowHotkey() => HotkeyBox.Text = Hotkey.Parse(_settings.Hotkey)?.ToString() ?? L.ShortcutNone;

    // ── About ─────────────────────────────────────────────────────────────

    private void OnSource(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(RepoUrl) { UseShellExecute = true });

    private void OnLog(object sender, RoutedEventArgs e)
    {
        if (File.Exists(Log.FilePath))
            Process.Start(new ProcessStartInfo(Log.FilePath) { UseShellExecute = true });
    }

    private void OnQuit(object sender, RoutedEventArgs e) => App.Current.Quit();
}
