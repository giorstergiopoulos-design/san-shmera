using System.Globalization;
using Chronikologio.Core;
using Opsis.UI;

namespace Chronikologio.UI;

/// <summary>
/// A frame that shows the events of a day one at a time (Greek or world): a title, a left and a right arrow with a counter, the photograph beside the text
/// and the sources under it. Every event shows its photograph and its source; when the database has no photograph yet the frame says so.
/// </summary>
public sealed class EventCarousel : Panel
{
    private readonly Label _title = new() { Dock = DockStyle.Fill, AutoSize = false, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold) };
    private readonly Label _counter = new() { Dock = DockStyle.Right, AutoSize = false, Width = 64, TextAlign = ContentAlignment.MiddleCenter, Tag = "muted" };
    private readonly FlatButton _left = new() { Text = "◀", Dock = DockStyle.Right, Width = 36 };
    private readonly FlatButton _right = new() { Text = "▶", Dock = DockStyle.Right, Width = 36 };
    private readonly PhotoBox _picture = new() { Dock = DockStyle.Left, Width = 180 };
    private readonly Label _noPicture = new() { Dock = DockStyle.Bottom, Height = 22, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Tag = "muted", Visible = false };
    private readonly Panel _picturePanel = new() { Dock = DockStyle.Left, Width = 180 };
    private readonly TextBox _text = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.None, TabStop = false };
    private readonly LinkLabel _source = new() { Dock = DockStyle.Bottom, Height = 58, AutoEllipsis = true, LinkBehavior = LinkBehavior.HoverUnderline };
    private readonly ToolTip _tip = new();

    private DayStore? _store;
    private IReadOnlyList<HistoricalEvent> _events = Array.Empty<HistoricalEvent>();
    private int _index;
    private string _empty = "";

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var stripe = new SolidBrush(ChronikologioTheme.Palette.Accent);
        e.Graphics.FillRectangle(stripe, 0, 0, 3, Height);   // the same accent stripe as the cards
    }

    public EventCarousel()
    {
        Tag = "card";
        Padding = new Padding(10, 6, 10, 6);
        Margin = new Padding(4);
        Dock = DockStyle.Fill;

        var header = new Panel { Dock = DockStyle.Top, Height = 32 };
        header.Controls.Add(_title);
        header.Controls.Add(_counter);
        header.Controls.Add(_left);    // docked right, one after the other: the one added last sits leftmost
        header.Controls.Add(_right);
        // the picture and its "none" label share the left column
        _picturePanel.Controls.Add(_picture);
        _picture.Dock = DockStyle.Fill;
        _picturePanel.Padding = new Padding(0, 4, 0, 0);   // a little air under the title line

        TextFit.ScrollWhenLonger(_text);   // a longer text than the frame scrolls
        Controls.Add(_text);
        Controls.Add(_picturePanel);
        Controls.Add(_noPicture);
        Controls.Add(_source);
        Controls.Add(header);

        _left.Click += (_, _) => Move(-1);
        _right.Click += (_, _) => Move(1);
        _source.LinkClicked += (_, e) =>
        {
            if (e.Link?.LinkData is string url && Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(u.AbsoluteUri) { UseShellExecute = true });
        };
        MouseWheel += (_, e) => Move(e.Delta < 0 ? 1 : -1);
        Resize += (_, _) => _picturePanel.Width = Math.Clamp(Width * 2 / 5, 110, 200);   // a narrow frame keeps most of its width for the text
    }

    public string FrameTitle
    {
        get => _title.Text;
        set => _title.Text = value;
    }

    public int Count => _events.Count;
    public int Index => _index;

    /// <summary>Shows the events of a day; the first one is shown. <paramref name="empty"/> is the text when the day has none.</summary>
    public void SetEvents(DayStore store, IReadOnlyList<HistoricalEvent> events, string empty)
    {
        _store = store;
        _events = events;
        _empty = empty;
        _index = 0;
        Show();
    }

    /// <summary>The left and right arrows: the previous or the next event, around the ends.</summary>
    public void Move(int step)
    {
        if (_events.Count < 2) return;
        _index = (_index + step + _events.Count) % _events.Count;
        Show();
    }

    private void Show()
    {
        _picture.Image?.Dispose();
        _picture.Image = null;
        _left.Enabled = _right.Enabled = _events.Count > 1;
        // with one event (or none) there is nothing to turn through: the title gets the room of the arrows and the counter
        _left.Visible = _right.Visible = _counter.Visible = _events.Count > 1;
        if (_events.Count == 0)
        {
            _counter.Text = "";
            _text.Text = _empty;
            _noPicture.Visible = false;
            _picturePanel.Visible = false;
            _source.Text = "";
            return;
        }
        var e = _events[_index];
        _counter.Text = $"{_index + 1} / {_events.Count}";
        _text.Text = ((e.Year is { } y ? y.ToString(CultureInfo.InvariantCulture) + " — " : "") + e.Title + "\r\n\r\n" + e.Summary).Replace("\n", Environment.NewLine).Replace(Environment.NewLine + Environment.NewLine + Environment.NewLine, Environment.NewLine + Environment.NewLine);
        _text.SelectionStart = 0;
        _text.SelectionLength = 0;

        var image = LoadImage(e, out var info);
        _picture.Image = image;
        _picture.Visible = image is not null;
        _picturePanel.Visible = image is not null;
        _noPicture.Text = Loc.T("event.nophoto");
        _noPicture.Visible = image is null;
        if (info is not null) _tip.SetToolTip(_picture, $"{info.Caption}\n{info.Creator} · {info.License}");
        SetSources(e);
    }

    private Image? LoadImage(HistoricalEvent e, out ImageRef? info)
    {
        info = null;
        if (_store is null || e.Images is null) return null;
        foreach (var candidate in e.Images)
        {
            var path = _store.ImagePath(candidate);
            if (path is null) continue;
            try
            {
                info = candidate;
                return ImageLoader.Load(path);
            }
            catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException) { }
        }
        return null;
    }

    private void SetSources(HistoricalEvent e)
    {
        // official references first, then the other links, then the written citation of a source without a link
        var linked = e.Sources.Where(s => !string.IsNullOrEmpty(s.Url))
            .OrderBy(s => s.Kind == "official-reference" ? 0 : s.Kind == "encyclopedia-pointer" ? 2 : 1).Take(3).ToList();
        _source.Links.Clear();
        if (linked.Count == 0)
        {
            var citation = e.Sources.FirstOrDefault()?.Citation ?? "";
            var cut = citation.IndexOf(". ", StringComparison.Ordinal);
            var shown = cut > 0 ? citation[..(cut + 1)] : citation;
            if (shown.Length > 100) shown = shown[..99].TrimEnd() + "…";
            _source.Text = Loc.T("event.source") + ": " + shown;
            _tip.SetToolTip(_source, citation);
            return;
        }
        var text = Loc.T("event.source") + ": ";
        var spans = new List<(int Start, int Length, string Url)>();
        foreach (var s in linked)
        {
            var host = new Uri(s.Url!).Host.Replace("www.", "");
            var label = s.Kind == "encyclopedia-pointer" ? host + " (" + Loc.T("event.more") + ")" : host;
            if (spans.Count > 0) text += " · ";
            spans.Add((text.Length, label.Length, s.Url!));
            text += label;
        }
        _source.Text = text;
        foreach (var (start, length, url) in spans) _source.Links.Add(start, length, url);
    }

    public void Restyle()
    {
        var p = ChronikologioTheme.Palette;
        _source.LinkColor = p.Accent;
        _source.ActiveLinkColor = p.Accent;
        _source.VisitedLinkColor = p.Accent;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _picture.Image?.Dispose(); _tip.Dispose(); }
        base.Dispose(disposing);
    }
}
