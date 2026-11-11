using System.Drawing.Drawing2D;
using System.Globalization;
using Chronikologio.Core;
using Opsis.UI;

namespace Chronikologio.UI;

/// <summary>A rounded label ("chip"): a name that celebrates, or a filter of the cards (selected = filled with the accent colour).</summary>
public sealed class Chip : Control
{
    private bool _selected, _hover;

    public Chip(bool clickable)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        Clickable = clickable;
        Cursor = clickable ? Cursors.Hand : Cursors.Default;
        Font = new Font("Segoe UI", 9.5f);
        Margin = new Padding(0, 0, 6, 6);
        BackColor = Color.Transparent;
        Size = new Size(60, 26);
    }

    public bool Clickable { get; }

    public bool Selected
    {
        get => _selected;
        set { _selected = value; Invalidate(); }
    }

    public override string Text
    {
        get => base.Text;
        set { base.Text = value; Width = TextRenderer.MeasureText(value, Font, new Size(int.MaxValue, 26), TextFormatFlags.NoPadding).Width + 22; Invalidate(); }
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover = true; Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = false; Invalidate(); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ChronikologioTheme.Palette;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
        using var path = Pill(r);
        var fill = _selected ? p.Accent : Color.FromArgb(_hover && Clickable ? 60 : 34, p.Text);
        using (var b = new SolidBrush(fill)) g.FillPath(b, path);
        if (!_selected) { using var pen = new Pen(Color.FromArgb(60, p.Text), 1f); g.DrawPath(pen, path); }
        var color = _selected ? DayLayout.OnAccent(p.Accent) : p.Text;
        TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }

    private static GraphicsPath Pill(RectangleF r)
    {
        var d = r.Height;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 90, 180);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 180);
        path.CloseFigure();
        return path;
    }

    public void Restyle() => Invalidate();
}

/// <summary>A card of the day: a photograph (cropped to fill the top of the card), the year, the title, a short text and the source. Without a photograph the card is lower.</summary>
public sealed class EventTile : Panel
{
    public const int TileWidth = 240;
    private readonly PhotoBox _photo = new() { Dock = DockStyle.Top, Height = 120, InsetRight = 0, Cover = true, Cursor = Cursors.Hand };
    private readonly Label _year = new() { Dock = DockStyle.Top, Height = 20, Tag = "muted" };
    private readonly Label _title = new() { Dock = DockStyle.Top, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold) };
    private readonly Label _text = new() { Dock = DockStyle.Fill };
    private readonly Label _source = new() { Dock = DockStyle.Bottom, Height = 20, Tag = "muted", Font = new Font("Segoe UI", 8.5f), AutoEllipsis = true };
    private readonly ToolTip _tip = new();
    private ImageRef? _info;
    private Image? _image;
    private string _creditPath = "";

    public EventTile(string year, string title, string text, string source, Image? image, ImageRef? info, string? photoPath)
    {
        Tag = "card";
        Width = TileWidth;
        Padding = new Padding(10, 8, 10, 8);
        Margin = new Padding(0, 0, 12, 12);
        _info = info;
        _image = image;
        _photo.CoverFocus = info?.Kind == "portrait" ? 0.06 : 0.3;   // a portrait keeps its top, where the face is
        if (info?.Kind == "portrait") { _photo.Cover = false; _photo.Contain = true; }   // and is not cropped at all
        _creditPath = photoPath ?? "";
        _year.Text = year;
        _title.Text = title;
        _fullText = text;
        _text.Text = Cut(text, 112);
        _source.Text = source;
        _title.Height = TextRenderer.MeasureText(title, _title.Font, new Size(TileWidth - 20, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + 6;
        Controls.Add(_text);
        Controls.Add(_source);
        Controls.Add(_title);
        Controls.Add(_year);
        if (image is not null)
        {
            _photo.Image = image;
            Padding = new Padding(0, 0, 0, 8);
            _year.Padding = new Padding(10, 0, 10, 0); _title.Padding = new Padding(10, 0, 10, 0); _text.Padding = new Padding(10, 0, 10, 0); _source.Padding = new Padding(10, 0, 10, 0);
            Controls.Add(_photo);
            if (info is not null) _tip.SetToolTip(_photo, $"{info.Caption}\n{info.Creator} · {info.License}\n{info.SourceUrl}");
            _photo.Click += (_, _) => PhotoDialog.ShowFor(FindForm(), image, info);
        }
        Height = (image is null ? 0 : 120) + 20 + _title.Height + 92 + 30;
    }

    public bool HasPhoto => _image is not null;

    private string _fullText = "";

    /// <summary>Makes the card as wide as given (the cards share the whole width of the window): a wider card has a taller photograph and shows more of the text.</summary>
    public void FitTo(int width)
    {
        Width = width;
        var photoH = _image is null ? 0 : Math.Clamp(width / 2, 110, 230);
        // a portrait is shown whole (never cropped): the box is as tall as the picture needs, up to 320 px, and the picture is scaled to fit it
        if (_image is not null && _info?.Kind == "portrait") photoH = Math.Clamp((int)(width * _image.Height / (double)_image.Width), 110, 320);
        if (_image is not null) _photo.Height = photoH;
        _text.Text = Cut(_fullText, (int)(112 * width / 240.0));
        var inner = Math.Max(60, width - 20);
        _title.Height = TextRenderer.MeasureText(_title.Text, _title.Font, new Size(inner, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + 6;
        var textH = TextRenderer.MeasureText(_text.Text, _text.Font, new Size(inner, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + 8;
        Height = photoH + 20 + _title.Height + Math.Max(40, textH) + 20 + 22;
    }

    /// <summary>Raised when the card is clicked anywhere (it opens the whole text).</summary>
    public event Action? Opened;

    private void WireClicks(Control c)
    {
        if (c is PhotoBox) return;   // the photograph has its own click: it opens large
        c.Click += (_, _) => Opened?.Invoke();
        c.Cursor = Cursors.Hand;
        foreach (Control child in c.Controls) WireClicks(child);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!_wired) { _wired = true; WireClicks(this); }
    }

    private bool _wired;

    private static string Cut(string text, int max)
    {
        text = text.Trim();
        if (text.Length <= max) return text;
        var at = text.LastIndexOf(' ', max - 1);
        return text[..(at > max / 2 ? at : max - 1)].TrimEnd(',', ';', ' ', '.') + "…";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var stripe = new SolidBrush(ChronikologioTheme.Palette.Accent);
        e.Graphics.FillRectangle(stripe, 0, 0, 3, Height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _image?.Dispose(); _tip.Dispose(); }
        base.Dispose(disposing);
    }
}

/// <summary>A photograph shown large, with its credit (creator, licence, source). Esc or a click closes it.</summary>
public sealed class PhotoDialog : Form
{
    private PhotoDialog(Image image, ImageRef? info)
    {
        var p = ChronikologioTheme.Palette;
        Text = info?.Caption is { Length: > 0 } c ? c : Loc.T("dialog.title");
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(960, 700);
        BackColor = p.Background; ForeColor = p.Text;
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        var picture = new PictureBox { Dock = DockStyle.Fill, Image = image, SizeMode = PictureBoxSizeMode.Zoom, BackColor = p.Background, Cursor = Cursors.Hand };
        picture.Click += (_, _) => Close();
        var credit = new Label
        {
            Dock = DockStyle.Bottom, Height = 64, Padding = new Padding(12, 6, 12, 6), ForeColor = p.TextMuted, BackColor = p.Surface,
            Text = info is null ? "" : $"{info.Caption}\n{info.Creator} · {info.License} · {info.SourceUrl}"
        };
        Controls.Add(picture);
        Controls.Add(credit);
    }

    public static void ShowFor(IWin32Window? owner, Image image, ImageRef? info)
    {
        using var dialog = new PhotoDialog(image, info);
        dialog.ShowDialog(owner);
    }
}

/// <summary>The whole text of a day (lives, sources, all events) in a window of its own: what "Read the life" opens.</summary>
public sealed class LifeDialog : Form
{
    private LifeDialog(DayStore store, DateOnly date)
    {
        var p = ChronikologioTheme.Palette;
        Text = date.ToString("d MMMM", Loc.Culture);
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(820, 680);
        BackColor = p.Background; ForeColor = p.Text;
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };
        var view = new DayDetailsView { Dock = DockStyle.Fill, BackColor = p.Background, ForeColor = p.Text, Padding = new Padding(12) };
        view.ShowDay(store, date, full: true);
        ThemeNative.StyleScrollbars(view);
        Controls.Add(view);
    }

    public static void ShowFor(IWin32Window? owner, DayStore store, DateOnly date)
    {
        using var dialog = new LifeDialog(store, date);
        dialog.ShowDialog(owner);
    }
}

/// <summary>
/// The day as one screen: the saint of the day as a large card (picture, short life, the names that celebrate as chips), a row of filters
/// (the country, the world, births, deaths) and the cards of what happened, with the small calendar at the right.
/// </summary>
public sealed class DayOverviewLayout : DayLayout
{
    private readonly MiniCalendar _mini;
    private readonly FlowLayoutPanel _stack = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(8, 8, 8, 8) };
    private readonly Panel _hero = new() { Tag = "card", Height = 176, Padding = new Padding(14, 12, 14, 10), Margin = new Padding(0, 0, 0, 12) };
    private readonly PhotoBox _heroPhoto = new() { Dock = DockStyle.Left, Width = 0, InsetRight = 16, Cursor = Cursors.Hand };
    private readonly Label _heroKicker = new() { Dock = DockStyle.Top, Height = 20, Tag = "muted" };
    private readonly Label _heroTitle = new() { Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI", 13f, FontStyle.Bold), AutoEllipsis = true };
    private readonly Label _heroText = new() { Dock = DockStyle.Top, Height = 64 };
    private readonly FlowLayoutPanel _names = new() { Dock = DockStyle.Top, Height = 34, WrapContents = true, Padding = new Padding(0, 4, 0, 0) };
    private readonly Label _others = new() { Dock = DockStyle.Bottom, Height = 22, Tag = "muted", AutoEllipsis = true };
    private readonly LinkLabel _read = new() { Dock = DockStyle.Bottom, Height = 24, LinkBehavior = LinkBehavior.HoverUnderline };
    private readonly Panel _band = new() { Tag = "card", Height = 92, Margin = new Padding(0, 0, 0, 8) };   // the filters: no moving background here, the waves live only in the header
    private readonly Label _bandText = new() { Left = 14, Top = 8, Height = 22, AutoSize = false, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold) };
    private readonly FlowLayoutPanel _filters = new() { Left = 10, Top = 40, Height = 40, WrapContents = false, AutoSize = false };
    private readonly YearTimeline _timeline = new() { Margin = new Padding(0, 0, 0, 10) };
    private readonly FlowLayoutPanel _cards = new() { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Margin = new Padding(0) };
    private readonly Chip[] _chips = { new(true), new(true), new(true), new(true) };
    private Image? _heroImage;
    private int _filter;
    private DayView? _day;

    public DayOverviewLayout(DayStore store) : base(store)
    {
        _mini = new MiniCalendar(store) { Dock = DockStyle.Right, Width = 240 };
        _mini.DateChosen += ChangeDate;

        // the saint of the day (dock order: the last control added is laid out first)
        var text = new Panel { Dock = DockStyle.Fill };
        text.Controls.Add(_names);
        text.Controls.Add(_heroText);
        text.Controls.Add(_heroTitle);
        text.Controls.Add(_heroKicker);
        text.Controls.Add(_read);
        text.Controls.Add(_others);
        _hero.Controls.Add(text);
        _hero.Controls.Add(_heroPhoto);
        _heroTitle.Padding = new Padding(0); _heroText.AutoSize = false;
        _read.LinkClicked += (_, _) => LifeDialog.ShowFor(FindForm(), Store, SelectedDate);
        _heroPhoto.Click += (_, _) => { if (_heroImage is not null) PhotoDialog.ShowFor(FindForm(), _heroImage, _heroInfo); };

        foreach (var chip in _chips) _filters.Controls.Add(chip);
        _band.Controls.Add(_bandText);
        _band.Controls.Add(_filters);
        _timeline.MarkClicked += OnMarkClicked;
        for (var i = 0; i < _chips.Length; i++)
        {
            var index = i;
            _chips[i].Click += (_, _) => { _filter = index; ShowCards(); };
        }
        _stack.Controls.Add(_hero);
        _stack.Controls.Add(_band);
        _stack.Controls.Add(_timeline);
        _stack.Controls.Add(_cards);
        _stack.Resize += (_, _) => FitWidths();

        Controls.Add(_stack);
        Controls.Add(_mini);
        Controls.Add(Header);
    }

    private ImageRef? _heroInfo;

    public override string Name => "day";

    protected override void StoreChanged() => _mini.SetStore(Store);

    private int _heroNames;

    // the card is as high as its parts need: the chips of the names may take one or two rows
    private void FitHero()
    {
        var w = Math.Max(200, _hero.Width - _heroPhoto.Width - 40);
        _names.Width = w;
        var wanted = _heroNames == 0 ? 0 : Math.Min(72, Math.Max(34, _names.GetPreferredSize(new Size(w, 0)).Height + 4));
        _names.Height = wanted;
        var textH = _heroText.Text.Length == 0 ? 0 : TextRenderer.MeasureText(_heroText.Text, _heroText.Font, new Size(Math.Max(100, _heroText.Width), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + 10;
        _heroText.Height = Math.Max(24, textH);
        _hero.Height = Math.Max(176, 20 + 30 + _heroText.Height + wanted + 24 + 22 + 28);
    }

    private void FitWidths()
    {
        var w = Math.Max(300, _stack.ClientSize.Width - _stack.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth);
        _hero.Width = w;
        _band.Width = w;
        _bandText.Width = w - 28;
        _filters.Width = w - 20;
        _timeline.Width = w;
        _cards.MaximumSize = new Size(w, 0);
        _cards.Width = w;
        FitTiles(w);
        _heroText.Width = _hero.Width - _heroPhoto.Width - 40;
        FitHero();
    }

    protected override void ShowDay(DayView day)
    {
        _day = day;
        _mini.Selected = SelectedDate;
        ShowHero(day);
        var region = Store.Region;
        var domestic = day.Events.Where(e => Countries.IsDomestic(region, e)).ToList();
        var abroad = day.Events.Where(e => !Countries.IsDomestic(region, e)).ToList();
        _chips[0].Text = $"{Loc.TR("chip.domestic", region)} {domestic.Count}";
        _chips[1].Text = $"{Loc.T("chip.world")} {abroad.Count}";
        _chips[2].Text = $"{Loc.T("chip.births")} {day.People.Count}";
        _chips[3].Text = $"{Loc.T("chip.deaths")} {day.DeathList.Count}";
        // the first filter with something in it is shown, unless the user is looking at another one that has cards
        var counts = new[] { domestic.Count, abroad.Count, day.People.Count, day.DeathList.Count };
        if (_filter < 0 || _filter > 3 || counts[_filter] == 0) _filter = Array.FindIndex(counts, c => c > 0) is var first and >= 0 ? first : 0;
        ShowCards();
        FitWidths();
    }

    // the strip of days, the line of years and the text of the band follow the day and the chosen filter
    private void ShowStrips()
    {

        if (_day is null) return;
        var region = Store.Region;
        var marks = new List<YearTimeline.Mark>();
        var domestic = _day.Events.Where(e => Countries.IsDomestic(region, e)).ToList();
        var abroad = _day.Events.Where(e => !Countries.IsDomestic(region, e)).ToList();
        for (var i = 0; i < domestic.Count; i++) if (domestic[i].Year is { } y) marks.Add(new YearTimeline.Mark(y, domestic[i].Title, 0, i));
        for (var i = 0; i < abroad.Count; i++) if (abroad[i].Year is { } y) marks.Add(new YearTimeline.Mark(y, abroad[i].Title, 1, i));
        for (var i = 0; i < _day.People.Count; i++) marks.Add(new YearTimeline.Mark(_day.People[i].Year, _day.People[i].Name, 2, i));
        for (var i = 0; i < _day.DeathList.Count; i++) marks.Add(new YearTimeline.Mark(_day.DeathList[i].Year, _day.DeathList[i].Name, 3, i));
        _timeline.SetMarks(marks);

        var count = new[] { domestic.Count, abroad.Count, _day.People.Count, _day.DeathList.Count }[Math.Clamp(_filter, 0, 3)];
        var years = marks.Where(m => m.Kind == _filter).Select(m => m.Year).ToList();
        var name = new[] { Loc.TR("chip.domestic", region), Loc.T("chip.world"), Loc.T("chip.births"), Loc.T("chip.deaths") }[Math.Clamp(_filter, 0, 3)];
        var span = years.Count > 0 ? $" · {Year(years.Min())} – {Year(years.Max())}" : "";
        _bandText.Text = $"{Loc.T("band.on")} {SelectedDate.ToString("d MMMM", Loc.Culture)} · {name}: {count}{span}";
    }

    private static string Year(int y) => y < 0 ? $"{-y} π.Χ." : y.ToString(CultureInfo.InvariantCulture);

    private void OnMarkClicked(YearTimeline.Mark mark)
    {
        _filter = mark.Kind;
        ShowCards();
        if (mark.Index >= 0 && mark.Index < _cards.Controls.Count)
        {
            var tile = _cards.Controls[mark.Index];
            _stack.ScrollControlIntoView(tile);
        }
    }

    // the cards share the whole width: as many columns as fit (at least 240 px each), the cards a little wider to fill the row
    private void FitTiles(int w)
    {
        var tiles = _cards.Controls.OfType<EventTile>().ToList();
        if (tiles.Count == 0) return;
        var columns = Math.Min(tiles.Count, Math.Clamp(w / 262, 1, 8));
        var width = Math.Min(560, (w - columns * 12 - 2) / columns);   // every card has 12 px of margin at its right
        foreach (var tile in tiles) tile.FitTo(Math.Max(220, width));
    }

    private void ShowHero(DayView day)
    {
        var p = ChronikologioTheme.Palette;
        _heroImage?.Dispose();
        _heroImage = null; _heroInfo = null;
        _heroPhoto.Image = null; _heroPhoto.Width = 0;
        foreach (Control c in _names.Controls.Cast<Control>().ToList()) { _names.Controls.Remove(c); c.Dispose(); }

        var ordered = day.Commemorations.OrderByDescending(c => c.IsMajorFeast).ToList();
        var main = ordered.FirstOrDefault();
        _heroKicker.Text = Loc.T("hero.kicker");
        if (main is null)
        {
            _heroTitle.Text = Loc.T("short.noentries");
            _heroText.Text = "";
            _read.Text = ""; _others.Text = "";
            return;
        }
        _heroTitle.Text = main.Title;
        _heroText.Text = Cut(string.IsNullOrWhiteSpace(main.Details) ? main.Summary : main.Summary, 260);
        foreach (var candidate in day.SaintImages)
        {
            var path = Store.ImagePath(candidate);
            if (path is null) continue;
            try
            {
                _heroImage = ImageLoader.Load(path);
                _heroInfo = candidate;
                var h = 136;
                _heroPhoto.Width = Math.Min(170, (int)Math.Round(_heroImage.Width * (h / (double)_heroImage.Height))) + _heroPhoto.InsetRight;
                _heroPhoto.Image = _heroImage;
                break;
            }
            catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException) { }
        }
        var names = day.Names.Select(n => n.Name).Distinct().ToList();
        var shown = _hero.Width > 700 ? 8 : 4;
        foreach (var n in names.Take(shown)) _names.Controls.Add(new Chip(false) { Text = n });
        if (names.Count > shown) _names.Controls.Add(new Chip(false) { Text = $"+{names.Count - shown}" });
        _heroNames = names.Count;
        FitHero();
        _read.Links.Clear();
        _read.Text = Loc.T("hero.read");
        _read.Links.Add(0, _read.Text.Length);
        _read.LinkColor = p.Accent; _read.ActiveLinkColor = p.Accent; _read.VisitedLinkColor = p.Accent;
        _others.Text = ordered.Count > 1 ? string.Format(Loc.T("hero.others"), ordered.Count - 1, string.Join(" · ", ordered.Skip(1).Take(3).Select(c => c.Title))) : "";
    }

    private static string Cut(string text, int max)
    {
        text = text.Trim();
        if (text.Length <= max) return text;
        var at = text.LastIndexOf(' ', max - 1);
        return text[..(at > max / 2 ? at : max - 1)].TrimEnd(',', ';', ' ', '.') + "…";
    }

    private void ShowCards()
    {
        for (var i = 0; i < _chips.Length; i++) _chips[i].Selected = i == _filter;
        ShowStrips();
        foreach (Control c in _cards.Controls.Cast<Control>().ToList()) { _cards.Controls.Remove(c); c.Dispose(); }
        if (_day is null) return;
        var region = Store.Region;
        IEnumerable<HistoricalEvent> events = _filter switch
        {
            0 => _day.Events.Where(e => Countries.IsDomestic(region, e)),
            1 => _day.Events.Where(e => !Countries.IsDomestic(region, e)),
            _ => Array.Empty<HistoricalEvent>()
        };
        if (_filter <= 1)
        {
            var list = events.ToList();
            if (list.Count == 0) _cards.Controls.Add(new Label { Text = _filter == 0 ? Loc.TR("event.none.gr", region) : Loc.T("event.none.world"), AutoSize = true, Tag = "muted", Padding = new Padding(2, 8, 0, 0) });
            foreach (var e in list) { var tile = TileFor(e); var ev = e; tile.Opened += () => OpenEvent(ev); _cards.Controls.Add(tile); }
        }
        else
        {
            var people = _filter == 2 ? _day.People : _day.DeathList;
            if (people.Count == 0) _cards.Controls.Add(new Label { Text = Loc.T("short.noentries"), AutoSize = true, Tag = "muted", Padding = new Padding(2, 8, 0, 0) });
            foreach (var person in people) { var (portrait, portraitInfo, portraitPath) = LoadFirst(person.Images); var tile = new EventTile(person.YearText, person.Name, person.Description, portraitInfo is null ? "" : Loc.T("event.source") + ": commons.wikimedia.org", portrait, portraitInfo, portraitPath); var who = person; tile.Opened += () => TextDialog.Show(FindForm(), who.Name, who.YearText + " — " + who.Name, who.Description); _cards.Controls.Add(tile); }
        }
        FitTiles(Math.Max(300, _stack.ClientSize.Width - _stack.Padding.Horizontal - SystemInformation.VerticalScrollBarWidth));
        Paint(_cards, ChronikologioTheme.Palette, false);   // only the new cards: RestyleAll would show the day again
    }

    // the whole text of an event: what the card has cut, the longer text and the sources
    private void OpenEvent(HistoricalEvent e)
    {
        var sb = new System.Text.StringBuilder(e.Summary);
        if (!string.IsNullOrWhiteSpace(e.Details)) sb.Append("\n\n").Append(e.Details);
        sb.Append("\n\n").Append(Loc.T("details.sources").Trim()).Append(": ");
        sb.Append(string.Join("  ·  ", e.Sources.Select(s => s.Url ?? s.Citation)));
        TextDialog.Show(FindForm(), e.Title, (e.Year is { } y ? Year(y) + " — " : "") + e.Title, sb.ToString().Replace("\n", Environment.NewLine));
    }

    private (Image? Image, ImageRef? Info, string? Path) LoadFirst(ImageRef[]? images)
    {
        foreach (var candidate in images ?? Array.Empty<ImageRef>())
        {
            var path = Store.ImagePath(candidate);
            if (path is null) continue;
            try { return (ImageLoader.Load(path), candidate, path); }
            catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException) { }
        }
        return (null, null, null);
    }

    private EventTile TileFor(HistoricalEvent e)
    {
        Image? image = null; ImageRef? info = null; string? path = null;
        foreach (var candidate in e.Images ?? Array.Empty<ImageRef>())
        {
            path = Store.ImagePath(candidate);
            if (path is null) continue;
            try { image = ImageLoader.Load(path); info = candidate; break; }
            catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException) { path = null; }
        }
        var link = e.Sources.FirstOrDefault(s => !string.IsNullOrEmpty(s.Url));
        var source = link is null ? Loc.T("event.source") + ": " + (e.Sources.FirstOrDefault()?.Citation.Split(". ")[0] ?? "") : Loc.T("event.source") + ": " + new Uri(link.Url!).Host.Replace("www.", "");
        return new EventTile(e.Year is { } y ? y.ToString(CultureInfo.InvariantCulture) : "", e.Title, e.Summary, source, image, info, path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _heroImage?.Dispose();
        base.Dispose(disposing);
    }
}
