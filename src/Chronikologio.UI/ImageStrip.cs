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

    private DayStore? _lastStore;
    private List<ImageRef> _lastImages = new();
    private int _lastHeight;

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        // a column beside the text must fit the height it has: the picture shrinks instead of pushing its credit out of sight
        if (Vertical && _lastStore is not null && _lastImages.Count > 0 && Math.Abs(Height - _lastHeight) > 6) Show(_lastStore, _lastImages);
    }

    public void Show(DayStore store, IEnumerable<ImageRef> images)
    {
        _lastStore = store;
        _lastImages = images.ToList();
        _lastHeight = Height;
        images = _lastImages;
        Clear();
        FlowDirection = Vertical ? FlowDirection.TopDown : FlowDirection.LeftToRight;
        foreach (var image in images)
        {
            var path = store.ImagePath(image);
            if (path is null) continue;
            Image bitmap;
            try
            {
                bitmap = ImageLoader.Load(path);   // read completely, so the file is not locked while the image is shown
            }
            catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException)
            {
                continue;
            }
            _loaded.Add(bitmap);

            // Horizontal strip: fixed height. Vertical column (beside the text): fixed width.
            var width = Vertical ? ColumnWidth : Math.Max(120, (int)(bitmap.Width * (double)ThumbnailHeight / Math.Max(1, bitmap.Height)));
            var height = Vertical ? Math.Min(720, (int)(bitmap.Height * (double)ColumnWidth / Math.Max(1, bitmap.Width))) : ThumbnailHeight;
            if (Vertical && Height > 120)
            {
                // the room left for the picture: the height of the column, less the credit under it and the gaps
                var creditText = $"{image.Caption}\n{image.Creator} · {image.License}\n{ShortUrl(image.SourceUrl)}";
                var room = Height - Padding.Vertical - 16;
                using var small = new Font(Font.FontFamily, 7.5f);
                for (var pass = 0; pass < 2 && height > 60; pass++)
                {
                    var creditH = TextRenderer.MeasureText(creditText, small, new Size(width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + 8;
                    if (height + creditH <= room) break;
                    height = Math.Max(60, room - creditH);
                    width = Math.Max(60, Math.Min(ColumnWidth, (int)(bitmap.Width * (double)height / Math.Max(1, bitmap.Height))));
                }
            }
            var box = new PhotoBox { Image = bitmap, Size = new Size(width, height), Margin = new Padding(0, 0, 0, 4), InsetRight = 0 };
            var credit = new Label
            {
                Text = $"{image.Caption}\n{image.Creator} · {image.License}\n{ShortUrl(image.SourceUrl)}",
                AutoSize = false,
                ForeColor = ForeColor,
                Font = new Font(Font.FontFamily, 7.5f)
            };
            credit.Size = new Size(width, TextRenderer.MeasureText(credit.Text, credit.Font, new Size(width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + 14);
            var cell = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = Vertical ? Padding.Empty : new Padding(0, 0, 10, 0) };   // a column that is as wide as the strip must not get a horizontal scroll bar
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
