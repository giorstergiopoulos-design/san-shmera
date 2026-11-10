using System.Drawing.Drawing2D;

namespace Chronikologio.UI;

/// <summary>
/// A picture box for the photographs and icons of the database: the image is scaled to fit (never enlarged beyond its size), kept at the top left of the
/// box, drawn smoothly with rounded corners and a thin border in the colour of the text, and a gap (<see cref="InsetRight"/>) is left between it and the text beside it.
/// </summary>
public sealed class PhotoBox : PictureBox
{
    /// <summary>Space kept free at the right of the picture, so that the text beside it does not touch it.</summary>
    public int InsetRight { get; set; } = 12;

    /// <summary>The rectangle the image is drawn in (inside the box), or empty when there is no image.</summary>
    public Rectangle ImageBounds
    {
        get
        {
            if (Image is null) return Rectangle.Empty;
            var availableW = Math.Max(1, Width - InsetRight);
            var availableH = Math.Max(1, Height);
            var scale = Math.Min(1.0, Math.Min(availableW / (double)Image.Width, availableH / (double)Image.Height));
            // small icons are allowed to grow a little, so a narrow portrait does not look lost in its column
            if (Image.Width * scale < availableW * 0.6 && Image.Height * scale < availableH) scale = Math.Min(Math.Min(availableW / (double)Image.Width, availableH / (double)Image.Height), scale * 1.6);
            return new Rectangle(0, 0, Math.Max(1, (int)Math.Round(Image.Width * scale)), Math.Max(1, (int)Math.Round(Image.Height * scale)));
        }
    }

    /// <summary>True: the image fills the whole box and the edges that do not fit are cropped (the top of a card); false: the whole image is shown.</summary>
    public bool Cover { get; set; }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        if (Image is null) return;
        if (Cover)
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            var box = new Rectangle(0, 0, Width, Height);
            var scale = Math.Max(box.Width / (double)Image.Width, box.Height / (double)Image.Height);
            var sw = (int)Math.Round(box.Width / scale); var sh = (int)Math.Round(box.Height / scale);
            var src = new Rectangle((Image.Width - sw) / 2, Math.Max(0, (int)((Image.Height - sh) * 0.3)), sw, sh);   // a little above the middle: faces and tops of buildings stay in
            g.DrawImage(Image, box, src, GraphicsUnit.Pixel);
            return;
        }
        var r = ImageBounds;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using var path = Rounded(new RectangleF(r.X, r.Y, r.Width - 1, r.Height - 1), 5f);
        var clip = g.Clip;
        g.SetClip(path, CombineMode.Intersect);
        g.DrawImage(Image, r);
        g.Clip = clip;
        using var pen = new Pen(Color.FromArgb(70, ForeColor), 1f);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
