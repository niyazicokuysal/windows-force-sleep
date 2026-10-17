using Microsoft.Win32;

namespace ForceSleep;

/// <summary>Per-user "start with Windows" via the HKCU Run key; needs no admin rights.</summary>
internal static class AutoStart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ForceSleep";

    /// <summary>Marks launches made by Windows at sign-in, which should start silently.</summary>
    public const string Argument = "--autostart";

    private static string Command => $"\"{Environment.ProcessPath}\" {Argument}";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return string.Equals(key?.GetValue(ValueName) as string, Command, StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, Command);
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
