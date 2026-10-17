using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ForceSleep;

/// <summary>The crescent moon used for the tray and windows; assets/ForceSleep.ico is the same shape.</summary>
internal static class MoonIcon
{
    public static readonly Color Gold = Color.FromArgb(0xF5, 0xC5, 0x42);
    public static readonly Color Gray = Color.FromArgb(0x9A, 0xA4, 0xB1);

    public static Icon Create(int size, Color color)
    {
        using var bitmap = DrawCrescent(size, color);
        var handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            Native.DestroyIcon(handle);
        }
    }

    public static Bitmap DrawCrescent(int size, Color color)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        var pad = size * 0.06f;
        var d = size - 2 * pad;
        using (var brush = new SolidBrush(color))
            g.FillEllipse(brush, pad, pad, d, d);

        // Punch out an offset circle to leave a crescent.
        g.CompositingMode = CompositingMode.SourceCopy;
        using var clear = new SolidBrush(Color.Transparent);
        g.FillEllipse(clear, pad + d * 0.36f, pad - d * 0.22f, d, d);
        return bitmap;
    }
}
