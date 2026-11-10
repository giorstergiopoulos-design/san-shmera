using System.Globalization;
using Chronikologio.Core;
using Opsis.UI;

namespace Chronikologio.UI;

/// <summary>Loads the first available image of a day (the icon of the saint celebrated) from the embedded database.</summary>
public static class DayImages
{
    public static Image? First(DayStore store, DayView day, out ImageRef? image)
    {
        foreach (var candidate in day.SaintImages)
        {
            var path = store.ImagePath(candidate);
            if (path is null) continue;
            try
            {
                image = candidate;
                return ImageLoader.Load(path);
            }
            catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException) { }
        }
        image = null;
        return null;
    }
}

/// <summary>A flag that opens the menu of countries; choosing one raises <see cref="RegionChosen"/>.</summary>
public sealed class FlagButton : PictureBox
{
    private string _region = "gr";
    private readonly ToolTip _tip = new();

    public event Action<string>? RegionChosen;

    public FlagButton()
    {
        Size = new Size(44, 28);
        SizeMode = PictureBoxSizeMode.CenterImage;
        Cursor = Cursors.Hand;
        Click += (_, _) => ShowMenu();
    }

    public void SetRegion(string region)
    {
        _region = region;
        var old = Image;
        Image = Flags.Create(region);
        old?.Dispose();
        _tip.SetToolTip(this, Loc.T("flag.tip"));
    }

    private void ShowMenu()
    {
        var menu = new ContextMenuStrip { ShowImageMargin = true, ShowCheckMargin = false, Renderer = new FlatMenuRenderer() };
        foreach (var country in Countries.All)
        {
            var region = country.Region;
            menu.Items.Add(new ToolStripMenuItem(Loc.T("region." + region), Flags.Create(region, 24, 16), (_, _) => RegionChosen?.Invoke(region)) { Checked = region == _region });
        }
        menu.Show(this, new Point(0, Height));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { Image?.Dispose(); _tip.Dispose(); }
        base.Dispose(disposing);
    }
}

/// <summary>Month grid with its own title and arrows; used beside the text in the card and feed layouts.</summary>
public sealed class MiniCalendar : UserControl
{
    private readonly Label _title = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label _prev = new() { Text = "◀", Dock = DockStyle.Left, Width = 28, TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };
    private readonly Label _next = new() { Text = "▶", Dock = DockStyle.Right, Width = 28, TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };
    private readonly CalendarGrid _grid = new() { Dock = DockStyle.Fill };
    private DayStore _store;

    public event Action<DateOnly>? DateChosen;

    public MiniCalendar(DayStore store)
    {
        _store = store;
        var nav = new Panel { Dock = DockStyle.Top, Height = 26 };
        nav.Controls.Add(_title);
        nav.Controls.Add(_prev);
        nav.Controls.Add(_next);
        Controls.Add(_grid);
        Controls.Add(nav);
        _prev.Click += (_, _) => { _grid.Month = _grid.Month.AddMonths(-1); UpdateTitle(); };
        _next.Click += (_, _) => { _grid.Month = _grid.Month.AddMonths(1); UpdateTitle(); };
        _grid.SelectedChanged += d => { UpdateTitle(); DateChosen?.Invoke(d); };
        SetStore(store);
        Restyle();
        UpdateTitle();
    }

    public DateOnly Selected
    {
        get => _grid.Selected;
        set => _grid.Selected = value;
    }

    public void SetStore(DayStore store)
    {
        _store = store;
        _grid.HasData = d => { var v = _store.GetDay(d); return v.Commemorations.Count + v.Events.Count > 0; };
        _grid.IsMajor = d => _store.GetDay(d).Commemorations.Any(c => c.IsMajorFeast);
        _grid.IsDayOff = d => Holidays.IsDayOff(d, _store.Region);
        _grid.Invalidate();
    }

    public void UpdateTitle() => _title.Text = _grid.Month.ToString("MMMM yyyy", Loc.Culture);

    public void Restyle()
    {
        var p = ChronikologioTheme.Palette;
        BackColor = p.Background;
        foreach (var c in new Control[] { _title, _grid }) { c.BackColor = p.Background; c.ForeColor = p.Text; }
        foreach (var c in new[] { _prev, _next }) { c.BackColor = p.Background; c.ForeColor = p.TextMuted; }
        _title.Font = new Font(Font, FontStyle.Bold);
        _grid.Accent = p.Accent;
        _grid.Muted = p.TextMuted;
        _grid.Invalidate();
    }
}

/// <summary>A label that always stays on one line and ends with "…" when it does not fit (a plain Label wraps a long text onto a second line).</summary>
internal sealed class EllipsisLabel : Label
{
    public EllipsisLabel() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); AutoEllipsis = true; }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }
}

/// <summary>The top bar shared by the layouts: flag, previous and next day, the date, today, and the fasting line.</summary>
public sealed class DayHeader : Panel
{
    private readonly FlagButton _flag = new() { Dock = DockStyle.Left, Width = 50 };
    private readonly Label _prev = new() { Text = "◀", Dock = DockStyle.Left, Width = 30, TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };
    private readonly Label _next = new() { Text = "▶", Dock = DockStyle.Left, Width = 30, TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };
    private readonly Label _date = new() { Dock = DockStyle.Left, AutoSize = false, Width = 330, TextAlign = ContentAlignment.MiddleLeft };
    private readonly EllipsisLabel _old = new() { Dock = DockStyle.Fill, Tag = "muted" };
    private readonly Label _fast = new() { Dock = DockStyle.Bottom, Height = 22, AutoSize = false, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Tag = "muted" };
    private readonly ToolTip _tip = new();
    private readonly FlatButton _today = new() { Dock = DockStyle.Right, Width = 90 };

    public event Action? Previous;
    public event Action? Next;
    public event Action? Today;
    public event Action<string>? RegionChosen;

    public DayHeader()
    {
        Dock = DockStyle.Top;
        Height = 70;
        Padding = new Padding(4, 4, 4, 2);
        // dock order: the last added is laid out first; the old-calendar note takes what the date and the buttons leave
        Controls.Add(_old);
        Controls.Add(_today);
        Controls.Add(_date);
        Controls.Add(_next);
        Controls.Add(_prev);
        Controls.Add(_flag);
        Controls.Add(_fast);   // the second row, over the whole width
        _prev.Click += (_, _) => Previous?.Invoke();
        _next.Click += (_, _) => Next?.Invoke();
        _today.Click += (_, _) => Today?.Invoke();
        _flag.RegionChosen += r => RegionChosen?.Invoke(r);
        _date.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
        ApplyText();
    }

    public void ApplyText() => _today.Text = Loc.T("btn.today");

    /// <summary>Puts the search of the window in the first row, just left of the Today button.</summary>
    public void AttachSearch(Control search)
    {
        search.Dock = DockStyle.Right;
        Controls.Add(search);
        Controls.SetChildIndex(search, Controls.GetChildIndex(_today));   // laid out right after the Today button, so it sits on its left
    }

    public void DetachSearch(Control search) => Controls.Remove(search);

    public void Set(DateOnly date, DayView day, string region)
    {
        _date.Text = date.ToString("dddd d MMMM yyyy", Loc.Culture);
        _date.Width = TextRenderer.MeasureText(_date.Text, _date.Font).Width + 24;   // as wide as the date, so the rest of the row is left to the old calendar date
        _old.Text = OldCalendar.Note(date);
        var holiday = string.Join(" · ", day.HolidayList.Select(Holidays.Line));
        _fast.Text = holiday.Length > 0 ? holiday : day.Fasting ?? "";
        _tip.SetToolTip(_fast, _fast.Text);
        _flag.SetRegion(region);
    }

    public void Restyle()
    {
        var p = ChronikologioTheme.Palette;
        BackColor = p.Background;
        foreach (var c in new Control[] { _date, _flag }) { c.BackColor = p.Background; c.ForeColor = p.Text; }
        _fast.BackColor = p.Background; _fast.ForeColor = p.TextMuted;
        _old.BackColor = p.Background; _old.ForeColor = p.TextMuted;
        foreach (var c in new[] { _prev, _next }) { c.BackColor = p.Background; c.ForeColor = p.TextMuted; }
    }
}

/// <summary>
/// A way of showing a day. Three exist: cards (<see cref="CardsLayout"/>), a feed (<see cref="FeedLayout"/>) and tabs (<see cref="TabsLayout"/>);
/// the user picks one in the View menu. All share the data, the chosen day and the country.
/// </summary>
public abstract class DayLayout : UserControl
{
    protected DayStore Store;
    private DateOnly _date = DateOnly.FromDateTime(DateTime.Today);
    protected readonly DayHeader Header = new();

    public event Action<DateOnly>? DateChanged;
    public event Action<string>? RegionRequested;

    protected DayLayout(DayStore store)
    {
        Store = store;
        Header.Previous += () => ChangeDate(_date.AddDays(-1));
        Header.Next += () => ChangeDate(_date.AddDays(1));
        Header.Today += () => ChangeDate(DateOnly.FromDateTime(DateTime.Today));
        Header.RegionChosen += r => RegionRequested?.Invoke(r);
        Loc.Changed += OnLanguage;
        ChronikologioTheme.Changed += RestyleAll;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Loc.Changed -= OnLanguage;
            ChronikologioTheme.Changed -= RestyleAll;
        }
        base.Dispose(disposing);
    }

    public void AttachSearch(Control search) => Header.AttachSearch(search);

    public void DetachSearch(Control search) => Header.DetachSearch(search);

    /// <summary>Which of the three layouts this is: "cards", "feed" or "tabs".</summary>
    public abstract string Name { get; }

    public DateOnly SelectedDate
    {
        get => _date;
        set
        {
            if (value == _date) return;
            _date = value;
            Refresh_();
        }
    }

    /// <summary>The user chose a day: shows it and tells the window.</summary>
    protected void ChangeDate(DateOnly date)
    {
        if (date == _date) return;
        _date = date;
        Refresh_();
        DateChanged?.Invoke(date);
    }

    public void SetStore(DayStore store)
    {
        Store = store;
        StoreChanged();
        Refresh_();
    }

    protected virtual void StoreChanged() { }

    protected abstract void ShowDay(DayView day);

    private void Refresh_()
    {
        var day = Store.GetDay(_date);
        Header.Set(_date, day, Store.Region);
        ShowDay(day);
    }

    private void OnLanguage()
    {
        Header.ApplyText();
        LanguageChanged();
        Refresh_();
    }

    protected virtual void LanguageChanged() { }

    protected void RestyleAll()
    {
        var p = ChronikologioTheme.Palette;
        BackColor = p.Background;
        Header.Restyle();
        Paint(this, p, false);
        Refresh_();
    }

    /// <summary>Applies the palette to a control tree: cards and inputs take the surface colour, the rest the background. Self-styled controls are left alone.</summary>
    /// <summary>Black or white, whichever reads better on the accent colour.</summary>
    public static Color OnAccent(Color accent)
    {
        static double Lin(int v) { var s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        var l = 0.2126 * Lin(accent.R) + 0.7152 * Lin(accent.G) + 0.0722 * Lin(accent.B);
        return (l + 0.05) / 0.05 > 1.05 / (l + 0.05) ? Color.Black : Color.White;
    }

    protected static void Paint(Control c, OpsisPalette p, bool inCard)
    {
        var tag = c.Tag as string;
        var card = inCard || tag == "card";
        if (c is EventCarousel carousel) carousel.Restyle();
        // dark scroll bars (and light ones in a light theme) wherever a control can scroll
        if (c is ListView or RichTextBox or ScrollableControl { AutoScroll: true }) ThemeNative.StyleScrollbars(c);
        switch (c)
        {
            case DayHeader or CalendarPanel: return;
            case MiniCalendar mini: mini.Restyle(); return;
            case Chip chip: chip.Restyle(); return;
            case FlatButton: return;
            case PictureBox: c.BackColor = card ? p.Surface : p.Background; return;
            case ListView or TextBoxBase or ComboBox:
                c.BackColor = p.Surface; c.ForeColor = p.Text; break;
            default:
                c.BackColor = card ? p.Surface : p.Background;
                c.ForeColor = tag == "muted" ? p.TextMuted : p.Text;
                break;
        }
        foreach (Control child in c.Controls) Paint(child, p, card);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RestyleAll();
    }
}

/// <summary>A titled box with a body text (and optionally an image beside it).</summary>
internal sealed class CardBox : Panel
{
    private readonly Label _title = new() { Dock = DockStyle.Top, Height = 24, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold) };
    private readonly TextBox _body = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.None, TabStop = false };
    private readonly PhotoBox _picture = new() { Dock = DockStyle.Left, Width = 0 };
    private readonly ToolTip _tip = new();

    private Image? _fitImage;

    // the picture column is as wide as the picture is at the height of the card: no empty band between an icon and the text
    private void FitPicture()
    {
        if (_fitImage is null) { _picture.Width = 0; return; }
        var h = Math.Max(120, _picture.Height > 30 ? _picture.Height : 180);
        var w = Math.Min(190, (int)Math.Round(_fitImage.Width * (h / (double)_fitImage.Height)));
        _picture.Width = Math.Max(90, w) + _picture.InsetRight;
    }

    public CardBox()
    {
        Tag = "card";
        Padding = new Padding(10, 8, 10, 8);
        Margin = new Padding(4);
        Dock = DockStyle.Fill;
        Controls.Add(_body);
        Controls.Add(_picture);
        Controls.Add(_title);
        Resize += (_, _) => FitPicture();
    }

    // A thin accent stripe at the left edge ties the cards to the accent colour of the theme
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var stripe = new SolidBrush(ChronikologioTheme.Palette.Accent);
        e.Graphics.FillRectangle(stripe, 0, 0, 3, Height);
    }

    public void Set(string title, string text, Image? image = null, string? credit = null)
    {
        _title.Text = title;
        _body.Text = text.Replace("\n", Environment.NewLine);
        _body.SelectionStart = 0;
        _body.SelectionLength = 0;
        _picture.Image?.Dispose();
        _picture.Image = image;
        _fitImage = image;
        FitPicture();
        if (credit is not null) _tip.SetToolTip(_picture, credit);
    }
}

/// <summary>Two frames side by side, Greece and abroad, each showing the events of the day one at a time with left and right arrows.</summary>
public sealed class EventFrames : TableLayoutPanel
{
    public EventCarousel Greek { get; } = new();
    public EventCarousel World { get; } = new();

    public EventFrames()
    {
        ColumnCount = 2;
        RowCount = 1;
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        Controls.Add(Greek, 0, 0);
        Controls.Add(World, 1, 0);
    }

    public void Set(DayStore store, DayView day)
    {
        // the first frame is the history of the country of the flag, the second everything else
        Greek.FrameTitle = Loc.TR("frame.gr", store.Region);
        World.FrameTitle = Loc.T("frame.world");
        Greek.SetEvents(store, day.Events.Where(e => Countries.IsDomestic(store.Region, e)).ToList(), Loc.TR("event.none.gr", store.Region));
        World.SetEvents(store, day.Events.Where(e => !Countries.IsDomestic(store.Region, e)).ToList(), Loc.T("event.none.world"));
    }
}

/// <summary>Layout A: a header, the saints and the births, the two frames of events (Greece and abroad), and a small calendar on the right.</summary>
public sealed class CardsLayout : DayLayout
{
    private readonly CardBox _saint = new(), _births = new();
    private readonly EventFrames _events = new() { Dock = DockStyle.Fill };
    private readonly MiniCalendar _mini;

    public CardsLayout(DayStore store) : base(store)
    {
        _mini = new MiniCalendar(store) { Dock = DockStyle.Right, Width = 240 };
        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 230, ColumnCount = 2, RowCount = 1 };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        top.Controls.Add(_saint, 0, 0);
        top.Controls.Add(_births, 1, 0);
        var table = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        table.Controls.Add(_events);
        table.Controls.Add(top);
        Controls.Add(table);
        Controls.Add(_mini);
        Controls.Add(Header);
        _mini.DateChosen += ChangeDate;
    }

    public override string Name => "cards";

    protected override void StoreChanged() => _mini.SetStore(Store);

    protected override void ShowDay(DayView day)
    {
        _mini.Selected = SelectedDate;
        var names = ShortText.NamesLine(day);
        var saint = ShortText.SaintText(day);
        var image = DayImages.First(Store, day, out var info);
        _saint.Set(Loc.T("card.saint"), (names.Length > 0 ? names + "\n\n" : "") + saint, image, info is null ? null : $"{info.Caption}\n{info.Creator} · {info.License}");
        _events.Set(Store, day);
        _births.Set(Loc.T(day.DeathList.Count > 0 ? "card.people" : "details.births").Trim(), ShortText.PeopleText(day));
    }
}

/// <summary>Layout B: a strip of days on top, one reading column with the image beside the text, and a small calendar at the side.</summary>
public sealed class FeedLayout : DayLayout
{
    private readonly Label[] _days = Enumerable.Range(0, 7).Select(_ => new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand, Margin = new Padding(2) }).ToArray();
    private readonly ImageStrip _images = new() { Dock = DockStyle.Left, Width = 250, Vertical = true, Padding = new Padding(8) };
    private readonly DayDetailsView _text = new() { Dock = DockStyle.Fill, ShowEvents = false, ShowOldCalendar = false };
    private readonly EventFrames _events = new() { Dock = DockStyle.Bottom, Height = 270 };
    private readonly MiniCalendar _mini;

    public FeedLayout(DayStore store) : base(store)
    {
        _mini = new MiniCalendar(store) { Dock = DockStyle.Right, Width = 240 };
        var strip = new TableLayoutPanel { Dock = DockStyle.Top, Height = 54, ColumnCount = 7, RowCount = 1, Padding = new Padding(4) };
        for (var i = 0; i < 7; i++)
        {
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7));
            var index = i;
            _days[i].Click += (_, _) => ChangeDate(SelectedDate.AddDays(index - 3));
            strip.Controls.Add(_days[i], i, 0);
        }
        Controls.Add(_text);
        Controls.Add(_events);
        Controls.Add(_images);
        Controls.Add(_mini);
        Controls.Add(strip);
        Controls.Add(Header);
        _mini.DateChosen += ChangeDate;
    }

    public override string Name => "feed";

    protected override void StoreChanged() => _mini.SetStore(Store);

    protected override void ShowDay(DayView day)
    {
        var p = ChronikologioTheme.Palette;
        _mini.Selected = SelectedDate;
        for (var i = 0; i < 7; i++)
        {
            var d = SelectedDate.AddDays(i - 3);
            var selected = i == 3;
            _days[i].Text = d.ToString("ddd", Loc.Culture) + "\n" + d.Day;
            _days[i].BackColor = selected ? p.Accent : p.Surface;
            _days[i].ForeColor = selected ? OnAccent(p.Accent) : p.Text;
        }
        _images.BackColor = p.Background;
        _images.ForeColor = p.Text;
        _images.Show(Store, day.SaintImages);
        _images.Visible = _images.Count > 0;
        _text.BackColor = p.Background;
        _text.ForeColor = p.Text;
        _text.ShowDay(Store, SelectedDate, full: false);
        _events.Set(Store, day);
    }
}

/// <summary>Layout C: tabs (today, calendar, names, timeline of the month, fasts and feasts); the calendar is one tab among several.</summary>
public sealed class TabsLayout : DayLayout
{
    private readonly TabHost _tabs = new() { Dock = DockStyle.Fill };
    private readonly Panel _today = new(), _calendar = new(), _names = new(), _timeline = new(), _feasts = new();
    private readonly ImageStrip _images = new() { Dock = DockStyle.Left, Width = 330, ColumnWidth = 310, Vertical = true, Padding = new Padding(8) };
    private readonly DayDetailsView _text = new() { Dock = DockStyle.Fill, ShowEvents = false, ShowOldCalendar = false };
    private readonly EventFrames _events = new() { Dock = DockStyle.Bottom, Height = 270 };
    private readonly CalendarPanel _panel;
    private readonly TextBox _nameQuery = new() { Dock = DockStyle.Top, BorderStyle = BorderStyle.FixedSingle };
    private readonly ListView _nameList = NewList();
    private readonly ComboBox _month = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ListView _monthList = NewList();
    private readonly Label _easter = new() { Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleLeft, Tag = "muted" };
    private readonly ListView _feastList = NewList();
    private bool _filling;

    public TabsLayout(DayStore store, CalendarPanel calendar) : base(store)
    {
        _panel = calendar;
        _panel.Dock = DockStyle.Fill;
        _today.Controls.Add(_text);
        _today.Controls.Add(_events);
        _today.Controls.Add(_images);
        _calendar.Controls.Add(_panel);
        _names.Controls.Add(_nameList);
        _names.Controls.Add(_nameQuery);
        _timeline.Controls.Add(_monthList);
        _timeline.Controls.Add(_month);
        _feasts.Controls.Add(_feastList);
        _feasts.Controls.Add(_easter);
        _tabs.AddPages(_today, _calendar, _names, _timeline, _feasts);
        Controls.Add(_tabs);
        Controls.Add(Header);

        _nameQuery.TextChanged += (_, _) => FillNames();
        _nameList.DoubleClick += (_, _) => PickFrom(_nameList);
        _monthList.DoubleClick += (_, _) => PickFrom(_monthList);
        _month.SelectedIndexChanged += (_, _) => { if (!_filling) FillMonth(); };
        _tabs.SelectedChanged += () => { if (_tabs.Selected == _feasts) FillFeasts(); };
        ApplyTexts();
    }

    public override string Name => "tabs";

    protected override void Dispose(bool disposing)
    {
        // the calendar panel belongs to the window and outlives this layout
        if (disposing) _calendar.Controls.Remove(_panel);
        base.Dispose(disposing);
    }

    protected override void StoreChanged() { FillNames(); }

    protected override void LanguageChanged() => ApplyTexts();

    private static ListView NewList() => new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false, BorderStyle = BorderStyle.None, GridLines = false };

    private void ApplyTexts()
    {
        _today.Text = Loc.T("tab.today");
        _calendar.Text = Loc.T("tab.calendar");
        _names.Text = Loc.T("tab.names");
        _timeline.Text = Loc.T("tab.timeline");
        _feasts.Text = Loc.T("tab.feasts");
        _tabs.RefreshTitles();
        _nameQuery.PlaceholderText = Loc.T("search.names");
        SetColumns(_nameList, new[] { 190, 190, 360 }, Loc.T("col.name"), Loc.T("col.day"), Loc.T("col.meaning"));
        SetColumns(_monthList, new[] { 190, 190, 360 }, Loc.T("col.day"), Loc.T("col.feast"), Loc.T("col.event"));
        SetColumns(_feastList, new[] { 200, 430, 330 }, Loc.T("col.date"), Loc.T("col.feast"), Loc.T("col.kind"));
        _filling = true;
        _month.Items.Clear();
        for (var m = 1; m <= 12; m++) _month.Items.Add(new DateTime(2000, m, 1).ToString("MMMM", Loc.Culture));
        _month.SelectedIndex = SelectedDate.Month - 1;
        _filling = false;
    }

    private static void SetColumns(ListView list, int[] widths, params string[] titles)
    {
        list.Columns.Clear();
        for (var i = 0; i < titles.Length; i++) if (titles[i].Length > 0) list.Columns.Add(titles[i], widths[i]);
    }

    protected override void ShowDay(DayView day)
    {
        var p = ChronikologioTheme.Palette;
        _tabs.Restyle();
        _images.BackColor = p.Background;
        _images.Show(Store, day.SaintImages);
        _images.Visible = _images.Count > 0;
        _text.BackColor = p.Background;
        _text.ForeColor = p.Text;
        _text.ShowDay(Store, SelectedDate, full: true);
        _events.Set(Store, day);
        if (_panel.SelectedDate != SelectedDate) _panel.SelectedDate = SelectedDate;
        _filling = true;
        if (_month.Items.Count == 12) _month.SelectedIndex = SelectedDate.Month - 1;
        _filling = false;
        FillMonth();
        if (_nameList.Items.Count == 0) FillNames();
        if (_tabs.Selected == _feasts) FillFeasts();
    }

    private void FillNames()
    {
        var q = TextNorm.Fold(_nameQuery.Text.Trim());
        _nameList.BeginUpdate();
        _nameList.Items.Clear();
        foreach (var (month, day, name) in Store.AllNames()
                     .Where(n => q.Length == 0 || TextNorm.Fold(n.Name.Name).Contains(q) || n.Name.Variants.Any(v => TextNorm.Fold(v).Contains(q)))
                     .Take(600))
        {
            var item = new ListViewItem(new[] { name.Name, new DateTime(2000, month, day).ToString("d MMMM", Loc.Culture), name.Etymology.Meaning }) { Tag = new DateOnly(SelectedDate.Year, month, Math.Min(day, DateTime.DaysInMonth(SelectedDate.Year, month))) };
            _nameList.Items.Add(item);
        }
        _nameList.EndUpdate();
    }

    private void FillMonth()
    {
        var month = SelectedDate.Month;
        if (_month.SelectedIndex >= 0) month = _month.SelectedIndex + 1;
        _monthList.BeginUpdate();
        _monthList.Items.Clear();
        for (var d = 1; d <= DateTime.DaysInMonth(SelectedDate.Year, month); d++)
        {
            var v = Store.GetDay(month, d);
            var feast = v.Commemorations.OrderByDescending(c => c.IsMajorFeast).FirstOrDefault()?.Title ?? "";
            var ev = v.Events.FirstOrDefault();
            var evText = ev is null ? "" : (ev.Year is { } y ? y + " — " : "") + ev.Title;
            _monthList.Items.Add(new ListViewItem(new[] { d.ToString(CultureInfo.InvariantCulture), feast, evText }) { Tag = new DateOnly(SelectedDate.Year, month, d) });
        }
        _monthList.EndUpdate();
    }

    private void FillFeasts()
    {
        _feastList.BeginUpdate();
        _feastList.Items.Clear();
        _feastList.Groups.Clear();
        var year = SelectedDate.Year;
        var gFasts = new ListViewGroup(Loc.T("group.fasts"));
        var gMovable = new ListViewGroup(Loc.T("group.movable"));
        var gNational = new ListViewGroup(Loc.T("group.national"));
        var gMemory = new ListViewGroup(Loc.T("group.memory"));
        var gLocal = new ListViewGroup(Loc.T("group.local"));
        _feastList.Groups.AddRange(new[] { gFasts, gMovable, gNational, gMemory, gLocal });
        _feastList.ShowGroups = true;
        string Range(DateOnly a, DateOnly b) => a == b ? a.ToString("ddd d MMMM", Loc.Culture) : $"{a.ToString("d MMM", Loc.Culture)} – {b.ToString("d MMM", Loc.Culture)}";
        void Add(ListViewGroup group, DateOnly date, string text, string name, string detail) =>
            _feastList.Items.Add(new ListViewItem(new[] { text, name, detail }) { Group = group, Tag = date });

        if (year is >= 1900 and <= 2099)
        {
            _easter.Text = $"{Loc.T("tab.easter")} {year}: {Paschalion.OrthodoxEaster(year).ToString("dddd d MMMM", Loc.Culture)}";
            foreach (var p in Fasting.PeriodsFor(year)) Add(gFasts, p.Start, Range(p.Start, p.End), p.Name, p.Rule);
            foreach (var (date, feast) in Paschalion.Movable(year, Store.Region).OrderBy(m => m.Date))
                Add(gMovable, date, date.ToString("ddd d MMMM", Loc.Culture), feast.Title, "");
        }
        else _easter.Text = "";
        foreach (var h in Holidays.ForYear(year, Store.Region))
        {
            var group = h.Kind switch { HolidayKind.Local => gLocal, HolidayKind.Remembrance => gMemory, _ => gNational };
            var detail = h.Kind == HolidayKind.Local ? $"{h.Place}: {h.Note}" : h.Kind == HolidayKind.Public ? Loc.T("holiday.public") : h.Note ?? "";
            Add(group, h.Date, h.Date.ToString("ddd d MMMM", Loc.Culture), h.Name, detail);
        }
        _feastList.EndUpdate();
        _feastList.DoubleClick -= FeastDoubleClick;
        _feastList.DoubleClick += FeastDoubleClick;
    }

    private void FeastDoubleClick(object? sender, EventArgs e) => PickFrom(_feastList);

    private void PickFrom(ListView list)
    {
        if (list.SelectedItems.Count == 0 || list.SelectedItems[0].Tag is not DateOnly date) return;
        ChangeDate(date);
        _tabs.Selected = _today;
    }
}
