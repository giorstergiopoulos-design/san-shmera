using System.Globalization;
using Chronikologio.Core;
using Opsis.UI;

namespace Chronikologio.UI;

/// <summary>
/// The window of the stand-alone app: a name search and the calendar panel. The menu bar is hidden until Ctrl+M (like the other Opsis apps);
/// Ctrl+T changes the theme. The look comes from the shared Opsis.UI palettes through <see cref="ChronikologioTheme"/>.
/// </summary>
public sealed class MainWindow : Form
{
    private static CultureInfo El => Loc.Culture;

    private DayStore _store;
    private readonly string _dataRoot;
    private readonly UiSettings _settings;
    private readonly string? _settingsPath;
    private readonly SearchBar _searchBar = new();
    private readonly CalendarPanel _panel;
    private readonly Panel _host = new() { Dock = DockStyle.Fill };
    private readonly SideRail _rail = new();
    private readonly List<ToolStripMenuItem> _layoutItems = new();
    private DayLayout? _layout;
    private DateOnly _date = DateOnly.FromDateTime(DateTime.Today);
    private readonly MenuStrip _menu = new() { Dock = DockStyle.Top, Renderer = new FlatMenuRenderer(), Visible = false };
    private readonly List<ToolStripMenuItem> _themeItems = new();
    private readonly List<ToolStripMenuItem> _regionItems = new();
    private readonly List<ToolStripMenuItem> _languageItems = new();

    public MainWindow(string dataRoot, string? settingsPath = null)
    {
        _dataRoot = dataRoot;
        _settingsPath = settingsPath;
        _settings = UiSettings.Load(settingsPath);
        _store = DayStore.Load(dataRoot, _settings.Region);
        Loc.Set(Countries.Resolve(_settings.Language, _store.Region));

        Font = new Font("Segoe UI", 10f);
        // Wide enough for the side rail, the two columns of cards and the small calendar; the last size is remembered.
        MinimumSize = new Size(UiSettings.MinWidth, UiSettings.MinHeight);
        ClientSize = new Size(_settings.Width, _settings.Height);
        StartPosition = FormStartPosition.CenterScreen;
        if (_settings.Maximized) WindowState = FormWindowState.Maximized;
        KeyPreview = true;
        FormClosing += (_, _) => SaveWindowState();

        _panel = new CalendarPanel(_store) { Dock = DockStyle.Fill, SettingsPath = settingsPath };
        _panel.SelectedDateChanged += SetDate;
        _panel.StoreChanged += store =>
        {
            _store = store;
            _layout?.SetStore(store);
            _settings.Region = store.Region;   // the panel saved it already; keep this copy in step so later saves do not undo it
            _searchBar.SetStore(store);
            foreach (var item in _regionItems) item.Checked = (string?)item.Tag == store.Region;
        };
        ApplyLanguage();
        Loc.Changed += ApplyLanguage;
        Disposed += (_, _) => Loc.Changed -= ApplyLanguage;
        Controls.Add(_host);
        Controls.Add(_rail);
        Controls.Add(_menu);
        _searchBar.SetStore(_store);
        _searchBar.DateChosen += SetDate;
        MainMenuStrip = _menu;
        ChooseLayout(_settings.Layout);
        BuildRail();

        ChronikologioTheme.Changed += ApplyTheme;
        Disposed += (_, _) => ChronikologioTheme.Changed -= ApplyTheme;

        ChronikologioTheme.Set(_settings.Theme);
        _menuShown = _settings.MenuShown;
        _menu.Visible = _menuShown;
        ApplyTheme();
    }

    // Kept in a field: Control.Visible reads false while the window itself is not shown yet.
    private bool _menuShown;

    /// <summary>Is the menu bar shown (or going to be shown when the window opens).</summary>
    public bool MenuVisible => _menuShown;

    /// <summary>Ctrl+M: shows or hides the menu bar and remembers the choice.</summary>
    public void ToggleMenu()
    {
        _menuShown = !_menuShown;
        _menu.Visible = _menuShown;
        _settings.MenuShown = _menuShown;
        _settings.Save(_settingsPath);
    }

    /// <summary>Ctrl+T: the next theme of the list.</summary>
    public void CycleTheme() => ChooseTheme(ChronikologioTheme.Next());

    /// <summary>The calendar in use: "gr" (Church of Greece) or "cy" (Church of Cyprus).</summary>
    public string Region => _store.Region;

    private void SaveWindowState()
    {
        _settings.Maximized = WindowState == FormWindowState.Maximized;
        var size = WindowState == FormWindowState.Normal ? ClientSize : RestoreBounds.Size;
        if (WindowState == FormWindowState.Normal)
        {
            _settings.Width = Math.Clamp(size.Width, UiSettings.MinWidth, 4000);
            _settings.Height = Math.Clamp(size.Height, UiSettings.MinHeight, 3000);
        }
        _settings.Save(_settingsPath);
    }

    private void BuildRail()
    {
        _rail.SetItems(new[]
        {
            new SideRail.RailItem("today", "", Loc.T("menu.today") + " (Ctrl+Home)", GoToday),
            new SideRail.RailItem("theme", "", Loc.T("menu.theme") + " (Ctrl+T)", CycleTheme),
            new SideRail.RailItem("settings", "\uE713", Loc.T("menu.settings") + " (Ctrl+,)", ShowSettings),
            new SideRail.RailItem("help", "", Loc.T("menu.about") + " (F1)", ShowGuide)
        });
        _rail.Checked = Layout;
    }

    /// <summary>The way a day is shown: "cards", "feed" or "tabs".</summary>
    public string Layout => _layout?.Name ?? _settings.Layout;

    /// <summary>The day shown in every layout.</summary>
    public DateOnly SelectedDate => _date;

    /// <summary>Goes to today in every layout and the shared calendar, and makes the button of the rail light up for a moment so the click is seen even when today is already shown.</summary>
    public void GoToday()
    {
        SetDate(DateOnly.FromDateTime(DateTime.Today));
        _rail.Pulse("today");
    }

    /// <summary>Shows a day in the chosen layout and in the shared calendar.</summary>
    public void SetDate(DateOnly date)
    {
        _date = date;
        if (_layout is not null) _layout.SelectedDate = date;
        if (_panel.SelectedDate != date) _panel.SelectedDate = date;
    }

    /// <summary>Switches between the three layouts (cards, feed, tabs) and remembers the choice.</summary>
    public void ChooseLayout(string name)
    {
        if (!UiSettings.LayoutNames.Contains(name) || _layout?.Name == name) return;
        var old = _layout;
        old?.DetachSearch(_searchBar);   // before the old layout is disposed, so the search is not disposed with its header
        _host.Controls.Clear();
        old?.Dispose();
        _layout = name switch
        {
            "cards" => new CardsLayout(_store),
            "feed" => new FeedLayout(_store),
            "tabs" => new TabsLayout(_store, _panel),
            _ => new DayOverviewLayout(_store)
        };
        _layout.Dock = DockStyle.Fill;
        _layout.SelectedDate = _date;
        _layout.DateChanged += SetDate;
        _layout.RegionRequested += ChooseRegion;
        _host.Controls.Add(_layout);
        _layout.AttachSearch(_searchBar);   // in the top row of the layout, next to the Today button
        _settings.Layout = name;
        _settings.Save(_settingsPath);
        foreach (var item in _layoutItems) item.Checked = (string?)item.Tag == name;
        _rail.Checked = name;
    }

    /// <summary>Ctrl+L: the next layout of the list.</summary>
    public void CycleLayout() => ChooseLayout(UiSettings.LayoutNames[(UiSettings.LayoutNames.ToList().IndexOf(Layout) + 1) % UiSettings.LayoutNames.Count]);

    /// <summary>Switches the database and, with it, the interface language (the flag button of the panel does the same).</summary>
    public void ChooseRegion(string region)
    {
        _panel.ChooseRegion(region);
    }

    /// <summary>Texts of the window in the language of the chosen country; the menu is built again because its items carry the texts.</summary>
    private void ApplyLanguage()
    {
        Text = Loc.T("app.title");
        _searchBar.SetPlaceholder(Loc.T("search.placeholder"));
        _themeItems.Clear();
        _regionItems.Clear();
        _languageItems.Clear();
        _layoutItems.Clear();
        _menu.Items.Clear();
        BuildMenu();
        BuildRail();
        foreach (var item in _themeItems) item.Checked = (string?)item.Tag == ChronikologioTheme.Name;
    }

    /// <summary>The language of the interface: "auto" (that of the country), "el" or "en". Kept apart from the country and remembered.</summary>
    public void ChooseLanguage(string setting)
    {
        if (setting is not ("auto" or "el" or "en")) return;
        _settings.Language = setting;
        _settings.Save(_settingsPath);
        if (!Loc.Set(Countries.Resolve(setting, _store.Region))) return;
        foreach (var item in _languageItems) item.Checked = (string?)item.Tag == setting;
    }

    /// <summary>The language setting in use: "auto", "el" or "en".</summary>
    public string LanguageSetting => _settings.Language;

    public void ChooseTheme(string name)
    {
        if (!ChronikologioTheme.Set(name)) return;
        _settings.Theme = name;
        _settings.Save(_settingsPath);
    }

    /// <summary>The window's keyboard shortcuts; true when the key was one of them.</summary>
    public bool HandleShortcut(Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.M: ToggleMenu(); return true;
            case Keys.Control | Keys.T: CycleTheme(); return true;
            case Keys.Control | Keys.Home: GoToday(); return true;
            case Keys.Control | Keys.L: CycleLayout(); return true;
            case Keys.F1: ShowGuide(); return true;
            case Keys.Control | Keys.Oemcomma: ShowSettings(); return true;
            case Keys.Control | Keys.F: _searchBar.Focus(); return true;
            case Keys.Control | Keys.K: _searchBar.Focus(); return true;
            default: return false;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData) => HandleShortcut(keyData) || base.ProcessCmdKey(ref msg, keyData);

    private void BuildMenu()
    {
        var file = new ToolStripMenuItem(Loc.T("menu.file"));
        file.DropDownItems.Add(new ToolStripMenuItem(Loc.T("menu.exit"), null, (_, _) => Close()) { ShortcutKeyDisplayString = "Alt+F4" });

        var view = new ToolStripMenuItem(Loc.T("menu.view"));
        view.DropDownItems.Add(new ToolStripMenuItem(Loc.T("menu.today"), null, (_, _) => GoToday()) { ShortcutKeyDisplayString = "Ctrl+Home" });
        var layouts = new ToolStripMenuItem(Loc.T("menu.layout")) { ShortcutKeyDisplayString = "Ctrl+L" };
        foreach (var name in UiSettings.LayoutNames)
        {
            var item = new ToolStripMenuItem(Loc.T("layout." + name), null, (_, _) => ChooseLayout(name)) { Tag = name, Checked = name == Layout };
            _layoutItems.Add(item);
            layouts.DropDownItems.Add(item);
        }
        view.DropDownItems.Add(layouts);
        var themes = new ToolStripMenuItem(Loc.T("menu.theme")) { ShortcutKeyDisplayString = "Ctrl+T" };
        foreach (var name in ChronikologioTheme.Names)
        {
            var item = new ToolStripMenuItem(ChronikologioTheme.DisplayName(name), null, (_, _) => ChooseTheme(name)) { Tag = name };
            _themeItems.Add(item);
            themes.DropDownItems.Add(item);
        }
        view.DropDownItems.Add(themes);

        var calendar = new ToolStripMenuItem(Loc.T("menu.calendar"));
        foreach (var country in Countries.All)
        {
            var key = country.Region;
            var item = new ToolStripMenuItem(Loc.T("region." + key), Flags.Create(key, 24, 16), (_, _) => ChooseRegion(key)) { Tag = key, Checked = key == _store.Region };
            _regionItems.Add(item);
            calendar.DropDownItems.Add(item);
        }
        view.DropDownItems.Add(calendar);
        var languages = new ToolStripMenuItem(Loc.T("menu.language"));
        foreach (var code in new[] { "auto", "el", "en" })
        {
            var chosen = code;
            var item = new ToolStripMenuItem(Loc.T("language." + code), null, (_, _) => ChooseLanguage(chosen)) { Tag = code, Checked = code == _settings.Language };
            _languageItems.Add(item);
            languages.DropDownItems.Add(item);
        }
        view.DropDownItems.Add(languages);
        view.DropDownItems.Add(new ToolStripMenuItem(Loc.T("menu.settings"), null, (_, _) => ShowSettings()) { ShortcutKeyDisplayString = "Ctrl+," });
        view.DropDownItems.Add(new ToolStripMenuItem(Loc.T("menu.hidemenu"), null, (_, _) => ToggleMenu()) { ShortcutKeyDisplayString = "Ctrl+M" });

        var help = new ToolStripMenuItem(Loc.T("menu.help"));
        help.DropDownItems.Add(new ToolStripMenuItem(Loc.T("menu.guide"), null, (_, _) => ShowGuide()) { ShortcutKeyDisplayString = "F1" });
        help.DropDownItems.Add(new ToolStripMenuItem(Loc.T("menu.shortcuts"), null, (_, _) => ShowHelp()));
        help.DropDownItems.Add(new ToolStripSeparator());
        help.DropDownItems.Add(new ToolStripMenuItem(Loc.T("menu.changelog"), null, (_, _) => ShowChangelog()));
        help.DropDownItems.Add(new ToolStripMenuItem(Loc.T("menu.license"), null, (_, _) => ShowLicense()));

        _menu.Items.AddRange(new ToolStripItem[] { file, view, help });
    }

    private void ShowGuide() => HelpDialog.ShowFor(this, 0);

    /// <summary>Opens the settings window (Ctrl+, or the gear of the rail).</summary>
    public void ShowSettings() => SettingsDialog.ShowFor(this);

    /// <summary>Puts the look back to its defaults: system theme, the cards layout, automatic language, no menu bar. The country is kept.</summary>
    public void ResetLook()
    {
        ChooseTheme("system");
        ChooseLayout("day");
        ChooseLanguage("auto");
        if (MenuVisible) ToggleMenu();
    }

    private void ShowChangelog() => HelpDialog.ShowFor(this, 1);
    private void ShowLicense() => HelpDialog.ShowFor(this, 2);

    private void ShowHelp() => MessageBox.Show(this, Loc.T("help.text"), Loc.T("menu.shortcuts"), MessageBoxButtons.OK, MessageBoxIcon.Information);

    /// <summary>Restyles the window, the search box and the menu with the current palette, and the title bar with its dark or light mode.</summary>
    private void ApplyTheme()
    {
        var p = ChronikologioTheme.Palette;
        BackColor = p.Background;
        ForeColor = p.Text;
        _searchBar.Restyle();
        _menu.BackColor = p.Surface;
        _menu.ForeColor = p.Text;
        foreach (var item in _themeItems) item.Checked = (string?)item.Tag == ChronikologioTheme.Name;
        _rail.Invalidate();
        if (IsHandleCreated) TitleBar.Apply(Handle, p.IsDark);
        Invalidate(true);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        TitleBar.Apply(Handle, ChronikologioTheme.IsDark);
    }
}
