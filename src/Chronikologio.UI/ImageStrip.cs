using Chronikologio.Core;

namespace Chronikologio.UI;

/// <summary>A row of images with their credit line (creator, licence, source) under each one. Images that are missing on disk are skipped.</summary>
public sealed class ImageStrip : FlowLayoutPanel
{
    private readonly List<Image> _loaded = new();

    public ImageStrip()
    {
        AutoScroll = true;
        WrapContents = false;
        FlowDirection = FlowDirection.LeftToRight;
    }

    public int ThumbnailHeight { get; set; } = 150;

    /// <summary>True: images are stacked in a column (for placing beside the text). False: one row (above or below the text).</summary>
    public bool Vertical { get; set; }

    public int ColumnWidth { get; set; } = 300;

    public void Show(DayStore store, IEnumerable<ImageRef> images)
    {
        Clear();
        FlowDirection = Vertical ? FlowDirection.TopDown : FlowDirection.LeftToRight;
        foreach (var image in images)
        {
            var path = store.ImagePath(image);
            if (path is null) continue;
            Image bitmap;
            try
            {
                // Loaded through a stream so the file is not locked while the image is shown.
                using var stream = File.OpenRead(path);
                bitmap = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
            }
            catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException)
            {
                continue;
            }
            _loaded.Add(bitmap);

            // Horizontal strip: fixed height. Vertical column (beside the text): fixed width.
            var width = Vertical ? ColumnWidth : Math.Max(120, (int)(bitmap.Width * (double)ThumbnailHeight / Math.Max(1, bitmap.Height)));
            var height = Vertical ? Math.Min(720, (int)(bitmap.Height * (double)ColumnWidth / Math.Max(1, bitmap.Width))) : ThumbnailHeight;
            var box = new PictureBox { Image = bitmap, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(width, height), Margin = new Padding(0, 0, 0, 2) };
            var credit = new Label
            {
                Text = $"{image.Caption}\n{image.Creator} · {image.License}\n{ShortUrl(image.SourceUrl)}",
                AutoSize = false,
                ForeColor = ForeColor,
                Font = new Font(Font.FontFamily, 7.5f)
            };
            credit.Size = new Size(width, TextRenderer.MeasureText(credit.Text, credit.Font, new Size(width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + 4);
            var cell = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = new Padding(0, 0, 10, 0) };
            cell.Controls.Add(box);
            cell.Controls.Add(credit);
            Controls.Add(cell);
        }
    }

    /// <summary>The address of a source without the long file path, so the line under a photograph does not run out of its column.</summary>
    private static string ShortUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host.Replace("www.", "") : url;

    public int Count => Controls.Count;

    public void Clear()
    {
        foreach (Control c in Controls) c.Dispose();
        Controls.Clear();
        foreach (var i in _loaded) i.Dispose();
        _loaded.Clear();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Clear();
        base.Dispose(disposing);
    }
}
