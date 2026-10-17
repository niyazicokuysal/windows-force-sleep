using System.Text.Json;

namespace ForceSleep;

internal sealed class Settings
{
    public static string Directory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ForceSleep");

    private static string FilePath => Path.Combine(Directory, "settings.json");

    public bool HibernateFallback { get; set; } = true;

    /// <summary>UI language: "tr", "en", or null to follow Windows.</summary>
    public string? Language { get; set; }

    /// <summary>UI theme: "light", "dark", or null to follow Windows.</summary>
    public string? Theme { get; set; }

    /// <summary>Sleep the machine when Windows was supposed to but didn't (see <see cref="SleepPolicy"/>).</summary>
    public bool Watchdog { get; set; } = true;

    public bool IdleSleep { get; set; }

    public int IdleSleepMinutes { get; set; } = 30;

    /// <summary>Automatic sleeps wait while music or video is playing.</summary>
    public bool WaitForMedia { get; set; } = true;

    public bool NotifyUnexpectedWake { get; set; } = true;

    /// <summary>Show a few seconds' countdown with a cancel button before sleeping.</summary>
    public bool Countdown { get; set; } = true;

    public bool PauseMediaBeforeSleep { get; set; } = true;

    /// <summary>Global shortcut such as "Ctrl+Alt+End"; empty for none.</summary>
    public string? Hotkey { get; set; } = ForceSleep.Hotkey.Default;

    [System.Text.Json.Serialization.JsonIgnore]
    public AutoSleepSettings AutoSleep => new(Watchdog, IdleSleep, TimeSpan.FromMinutes(IdleSleepMinutes), WaitForMedia);

    /// <summary>Changes made from the "Neden uyumadı?" window, so they can be undone.</summary>
    public List<AppliedFix> AppliedFixes { get; set; } = [];

    public static Settings Load()
    {
        try
        {
            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return new Settings();
        }
    }

    public void Save()
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write($"Could not save settings: {e.Message}");
        }
    }
}
