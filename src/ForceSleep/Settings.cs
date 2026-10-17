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
