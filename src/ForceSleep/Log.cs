using System.Globalization;

namespace ForceSleep;

internal static class Log
{
    /// <summary>Every line starts with a timestamp in this format, followed by two spaces.</summary>
    public const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";

    private const long MaxBytes = 512 * 1024;
    private static readonly Lock Gate = new();

    public static string FilePath { get; } = Path.Combine(Settings.Directory, "log.txt");

    public static void Write(string message)
    {
        lock (Gate)
        {
            try
            {
                System.IO.Directory.CreateDirectory(Settings.Directory);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes)
                    File.Delete(FilePath);
                File.AppendAllText(FilePath, $"{DateTime.Now.ToString(TimestampFormat, CultureInfo.InvariantCulture)}  {message}{Environment.NewLine}");
            }
            catch (IOException)
            {
                // Logging must never break sleeping.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
