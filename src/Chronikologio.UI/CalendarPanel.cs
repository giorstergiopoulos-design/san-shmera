using System.Globalization;
using Chronikologio.Core;

namespace Chronikologio.UI;

/// <summary>Month navigation, the grid, and under it a small text frame with a short text for the chosen day. Used by the stand-alone window and by the Opsis widget.</summary>
public sealed class CalendarPanel : UserControl
{
    private static CultureInfo El => Loc.Culture;

    private DayStore _store;
    private readonly PictureBox _flag = new() { Dock = DockStyle.Left, Width = 40, SizeMode = PictureBoxSizeMode.CenterImage, Cursor = Cursors.Hand };
    private readonly Label _title = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label _prev = new() { Text = "◀", Dock = DockStyle.Left, Width = 32, TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };
    private readonly Label _next = new() { Text = "▶", Dock = DockStyle.Right, Width = 32, TextAlign = ContentAlignment.MiddleCenter, Cursor = Cursors.Hand };
    private readonly Opsis.UI.FlatButton _more = new() { Text = Loc.T("panel.more"), Dock = DockStyle.Bottom, Height = 32 };
    private readonly Label _date = new() { Dock = DockStyle.Top, Height = 24, TextAlign = ContentAlignment.BottomLeft };
    private readonly TextBox _short = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle, TabStop = false };
    private readonly PictureBox _thumb = new() { Dock = DockStyle.Left, Width = 0, SizeMode = PictureBoxSizeMode.Zoom, Cursor = Cursors.Hand };
    private readonly Panel _bottom = new() { Dock = DockStyle.Bottom, Height = 190 };
    private readonly ToolTip _tip = new();
    private readonly CalendarGrid _grid = new() { Dock = DockStyle.Fill };

    public CalendarPanel(DayStore store)
    {
        _store = store;
        Font = new Font("Segoe UI", 9.5f);
        MinimumSize = new Size(220, 300);
        var nav = new Panel { Dock = DockStyle.Top, Height = 28 };
        nav.Controls.Add(_title);
        nav.Controls.Add(_prev);
        nav.Controls.Add(_next);
        nav.Controls.Add(_flag);
        _flag.Click += (_, _) => ShowCountryMenu();
        ShowFlag();
        Loc.Changed += ApplyLanguage;

        // Dock order: the last control added is laid out first, so the fill control goes in first.
        Controls.Add(_grid);
        Controls.Add(_date);
        _bottom.Controls.Add(_short);
        _bottom.Controls.Add(_thumb);
        Controls.Add(_bottom);
        Controls.Add(_more);
        Controls.Add(nav);

        _grid.HasData = date => { var v = _store.GetDay(date); return v.Commemorations.Count + v.Events.Count > 0; };
        _grid.IsMajor = date => _store.GetDay(date).Commemorations.Any(c => c.IsMajorFeast);
        _grid.IsDayOff = date => Holidays.IsDayOff(date, _store.Region);
        _grid.SelectedChanged += d => { RefreshDay(); UpdateTitle(); SelectedDateChanged?.Invoke(d); };
        _prev.Click += (_, _) => { _grid.Month = _grid.Month.AddMonths(-1); UpdateTitle(); };
        _next.Click += (_, _) => { _grid.Month = _grid.Month.AddMonths(1); UpdateTitle(); };
        _more.Click += (_, _) => ShowFull();
        _short.DoubleClick += (_, _) => ShowFull();
        _thumb.Click += (_, _) => ShowFull();

        ApplyTheme();
        ChronikologioTheme.Changed += ApplyTheme;
        UpdateTitle();
        RefreshDay();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        // The text frame takes about a third of the height: enough lines to read, while the grid keeps its room.
        _bottom.Height = Math.Clamp(Height * 34 / 100, 120, 220);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ChronikologioTheme.Changed -= ApplyTheme;
            Loc.Changed -= ApplyLanguage;
            _flag.Image?.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>Path of the settings file the choice of country is saved to (null = the default file in %AppData%).</summary>
    public string? SettingsPath { get; set; }

    /// <summary>Raised after the country, and so the database and the language, was changed by the flag button.</summary>
    public event Action<DayStore>? StoreChanged;

    /// <summary>The calendar in use: "gr" or "cy".</summary>
    public string Region => _store.Region;

    /// <summary>
    /// Switches to the database of a country. The interface language follows: it is the first official language of that country.
    /// The choice is remembered, and hosts are told through <see cref="StoreChanged"/>.
    /// </summary>
    public void ChooseRegion(string region)
    {
        if (!DayStore.Regions.Contains(region) || region == _store.Region) return;
        var store = DayStore.Load(_store.DataRoot, region);
        var settings = UiSettings.Load(SettingsPath);
        Loc.Set(Countries.Resolve(settings.Language, region));
        SetStore(store);
        ShowFlag();
        settings.Region = region;
        settings.Save(SettingsPath);
        StoreChanged?.Invoke(store);
    }

    private void ShowFlag()
    {
        var old = _flag.Image;
        _flag.Image = Flags.Create(_store.Region);
        old?.Dispose();
        _tip.SetToolTip(_flag, Loc.T("flag.tip"));
    }

    private void ShowCountryMenu()
    {
        var menu = new ContextMenuStrip { ShowImageMargin = true, ShowCheckMargin = false, Renderer = new Opsis.UI.FlatMenuRenderer() };
        foreach (var country in Countries.All)
        {
            var region = country.Region;
            menu.Items.Add(new ToolStripMenuItem(Loc.T("region." + region), Flags.Create(region, 24, 16), (_, _) => ChooseRegion(region)) { Checked = region == _store.Region });
        }
        menu.Show(_flag, new Point(0, _flag.Height));
    }

    private void ApplyLanguage()
    {
        _more.Text = Loc.T("panel.more");
        _tip.SetToolTip(_flag, Loc.T("flag.tip"));
        UpdateTitle();
        RefreshDay();
        _grid.Invalidate();
    }

    /// <summary>Takes the colours of the current theme (the shared palettes of Opsis.UI, or the host's colours inside Opsis).</summary>
    private void ApplyTheme()
    {
        var p = ChronikologioTheme.Palette;
        ApplyColors(p.Background, p.Text, p.TextMuted, p.Accent, p.Surface);
        _more.Invalidate();
    }

    /// <summary>Raised when the chosen day changes (by a click or by code).</summary>
    public event Action<DateOnly>? SelectedDateChanged;

    public DateOnly SelectedDate
    {
        get => _grid.Selected;
        set => _grid.Selected = value;
    }

    /// <summary>Switches to another calendar (Greece or Cyprus): the grid and the texts are drawn again.</summary>
    public void SetStore(DayStore store)
    {
        _store = store;
        _grid.Invalidate();
        RefreshDay();
    }

    /// <summary>True when the chosen day has an image that loaded and is shown beside the short text.</summary>
    public bool HasThumbnail => _thumb.Image is not null;

    /// <summary>The text of the small frame (for tests and for hosts that want to read it).</summary>
    public string ShortTextShown => _short.Text;

    /// <summary>Colours of the host (the Opsis widget passes its theme).</summary>
    public void ApplyColors(Color back, Color fore, Color muted, Color accent, Color? surface = null)
    {
        BackColor = back; ForeColor = fore;
        foreach (var c in new Control[] { _title, _prev, _next, _grid, _date }) { c.BackColor = back; c.ForeColor = fore; }
        // The frame of the short text is a box in the surface colour (or a slightly different shade of the background).
        _short.BackColor = surface ?? Blend(back, fore, 0.08f);
        _short.ForeColor = fore;
        ThemeNative.StyleScrollbars(_short);
        _prev.ForeColor = _next.ForeColor = muted;
        _date.ForeColor = muted;
        _grid.Accent = accent; _grid.Muted = muted;
        _title.Font = new Font(Font, FontStyle.Bold);
        _date.Font = new Font(Font, FontStyle.Bold);
        _grid.Invalidate();
    }

    private static Color Blend(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    private void UpdateTitle() => _title.Text = _grid.Month.ToString("MMMM yyyy", El);

    private void RefreshDay()
    {
        _date.Text = _grid.Selected.ToString("dddd d MMMM", El) + " " + OldCalendar.Note(_grid.Selected);
        _short.Text = ShortText.Build(_store.GetDay(_grid.Selected), _store.Region).Replace("\n", Environment.NewLine);
        _short.SelectionStart = 0;
        _short.SelectionLength = 0;
        ShowThumbnail(_store.GetDay(_grid.Selected));
    }

    /// <summary>The first available image of the day (a saint's icon, else an event photograph) beside the short text; hidden when there is none.</summary>
    private void ShowThumbnail(DayView day)
    {
        _thumb.Image?.Dispose();
        _thumb.Image = null;
        _thumb.Width = 0;
        foreach (var image in day.SaintImages)
        {
            var path = _store.ImagePath(image);
            if (path is null) continue;
            try
            {
                _thumb.Image = ImageLoader.Load(path);
            }
            catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or IOException)
            {
                continue;
            }
            _thumb.Width = 110;
            _tip.SetToolTip(_thumb, $"{image.Caption}\n{image.Creator} · {image.License}");
            return;
        }
    }

    private void ShowFull()
    {
        using var form = new Form
        {
            Text = Loc.T("dialog.title"),
            Size = new Size(720, 640),
            StartPosition = FormStartPosition.CenterScreen,
            BackColor = BackColor,
            ForeColor = ForeColor,
            Font = Font
        };
        var view = new DayDetailsView { Dock = DockStyle.Fill, BackColor = BackColor, ForeColor = ForeColor };
        // The images stand in a column BESIDE the text (on the right), not above it.
        var strip = new ImageStrip { Dock = DockStyle.Right, Width = 340, Vertical = true, BackColor = BackColor, ForeColor = ForeColor, Padding = new Padding(8) };
        form.Controls.Add(view);
        var day = _store.GetDay(_grid.Selected);
        strip.Show(_store, day.SaintImages);
        if (strip.Count > 0)
        {
            form.Size = new Size(1060, 680);
            form.Controls.Add(strip);
        }
        view.ShowDay(_store, _grid.Selected, full: true);
        form.ShowDialog(FindForm());
    }
}
