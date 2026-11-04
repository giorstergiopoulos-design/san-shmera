using System.Drawing.Drawing2D;
using System.Reflection;

namespace Chronikologio.UI;

/// <summary>
/// The official flags of the countries of the menu: high-resolution images embedded in this assembly (see Flags/CREDITS.md),
/// scaled down smoothly to the size asked for. Flags are 3:2.
/// </summary>
public static class Flags
{
    private static readonly Dictionary<string, Bitmap?> Originals = new();

    private static Bitmap? Original(string region)
    {
        lock (Originals)
        {
            if (Originals.TryGetValue(region, out var cached)) return cached;
            Bitmap? bmp = null;
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Flags.{region}.png");
            if (stream is not null) bmp = new Bitmap(stream);
            return Originals[region] = bmp;
        }
    }

    public static Bitmap Create(string region, int width = 30, int height = 20)
    {
        var bmp = new Bitmap(width, height);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var original = Original(region);
        if (original is null) g.Clear(Color.Gray);
        else lock (Originals)   // a GDI+ image must not be drawn from two threads at once
            g.DrawImage(original, new Rectangle(0, 0, width, height), 0, 0, original.Width, original.Height, GraphicsUnit.Pixel);
        using var border = new Pen(Color.FromArgb(90, 128, 128, 128));
        g.DrawRectangle(border, 0, 0, width - 1, height - 1);
        return bmp;
    }
}
