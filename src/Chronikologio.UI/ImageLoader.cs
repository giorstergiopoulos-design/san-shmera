using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.PixelFormats;

namespace Chronikologio.UI;

/// <summary>
/// Loads the photographs and icons of the database: WebP (the format of the files since 0.5.0, decoded here because GDI+ does not read it) and, for
/// older or hand-added files, JPEG and PNG. The file is read completely first, so it is not locked while the image is shown. Failures are
/// reported as <see cref="ArgumentException"/> (what the callers already catch for a damaged file).
/// </summary>
public static class ImageLoader
{
    public static Image Load(string path)
    {
        byte[] bytes;
        try { bytes = File.ReadAllBytes(path); }
        catch (UnauthorizedAccessException ex) { throw new IOException(ex.Message, ex); }
        return IsWebp(bytes) ? DecodeWebp(bytes) : DecodeGdi(bytes);
    }

    private static bool IsWebp(byte[] b) => b.Length > 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P';

    private static Image DecodeGdi(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var original = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
        return new Bitmap(original);   // an independent copy: nothing keeps the stream
    }

    private static Image DecodeWebp(byte[] bytes)
    {
        try
        {
            using var image = SixLabors.ImageSharp.Image.Load<Bgra32>(bytes);
            var pixels = new byte[image.Width * image.Height * 4];
            image.CopyPixelDataTo(pixels);
            var bitmap = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
            var data = bitmap.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (var y = 0; y < image.Height; y++)
                    Marshal.Copy(pixels, y * image.Width * 4, data.Scan0 + y * data.Stride, image.Width * 4);
            }
            finally { bitmap.UnlockBits(data); }
            return bitmap;
        }
        catch (Exception ex) when (ex is SixLabors.ImageSharp.ImageFormatException or InvalidOperationException or NotSupportedException)
        {
            throw new ArgumentException("damaged WebP image: " + ex.Message, ex);
        }
    }
}
