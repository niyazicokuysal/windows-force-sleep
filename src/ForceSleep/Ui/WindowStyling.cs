using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ForceSleep.Ui;

internal static partial class WindowStyling
{
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr window, int attribute, in int value, int size);

    /// <summary>
    /// Borderless windows get square corners by default; ask Windows 11 for its rounded ones
    /// (ignored on Windows 10).
    /// </summary>
    public static void RoundCorners(Window window) => window.SourceInitialized += (_, _) =>
        DwmSetWindowAttribute(new WindowInteropHelper(window).Handle, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND, sizeof(int));
}
