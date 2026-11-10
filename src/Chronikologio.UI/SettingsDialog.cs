using Chronikologio.Core;
using Opsis.UI;

namespace Chronikologio.UI;

/// <summary>
/// The settings window: the look (theme, layout, menu bar) and the country and language, in cards in the colours of the theme. Every change is applied at once
/// through the window and saved; there is no OK button to forget. "Reset" returns the look to its defaults.
/// </summary>
public sealed class SettingsDialog : Form
{
    private readonly MainWindow _window;
    private readonly FlatComboBox _theme = NewCombo(), _layout = NewCombo(), _region = NewCombo(), _language = NewCombo();
    private readonly FlatCheckBox _menu = new() { AutoSize = true };
    private readonly Label _note = new() { AutoSize = false, Height = 34, Dock = DockStyle.Bottom, Tag = "muted" };
    private readonly Opsis.UI.FlatButton _reset = new() { Width = 190, Height = 34 };
    private readonly Opsis.UI.FlatButton _close = new() { Width = 110, Height = 34 };
    private readonly List<(Panel Card, Label Head)> _cards = new();
    private readonly List<Label> _labels = new();
    private bool _filling;

    private readonly Dictionary<FlatComboBox, List<Choice>> _choices = new();

    // the combo boxes of Opsis.UI: drawn in the colours of the theme, no white boxes
    private static FlatComboBox NewCombo() => new() { Width = 300, Height = 32 };

    public SettingsDialog(MainWindow window)
    {
        _window = window;
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 490);
        Padding = new Padding(16);
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        var look = AddCard(Loc.T("settings.group.look"),
            (Loc.T("settings.theme"), _theme), (Loc.T("settings.layout"), _layout), ("", _menu));
        var calendar = AddCard(Loc.T("settings.group.calendar"),
            (Loc.T("settings.region"), _region), (Loc.T("settings.language"), _language));

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 56, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0) };
        buttons.Controls.Add(_close);
        buttons.Controls.Add(_reset);
        // dock order: the last added is laid out first, so the card that must be on top is added last
        var stack = new Panel { Dock = DockStyle.Fill };
        calendar.Dock = DockStyle.Top; look.Dock = DockStyle.Top;
        stack.Controls.Add(calendar);
        stack.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 12 });   // the gap between the cards
        stack.Controls.Add(look);
        Controls.Add(stack);
        Controls.Add(_note);
        Controls.Add(buttons);

        Fill();
        _theme.SelectedIndexChanged += (_, _) => { if (!_filling && Chosen(_theme) is { } c) _window.ChooseTheme(c.Key); };
        _layout.SelectedIndexChanged += (_, _) => { if (!_filling && Chosen(_layout) is { } c) _window.ChooseLayout(c.Key); };
        _region.SelectedIndexChanged += (_, _) => { if (!_filling && Chosen(_region) is { } c) { _window.ChooseRegion(c.Key); Fill(); } };
        _language.SelectedIndexChanged += (_, _) => { if (!_filling && Chosen(_language) is { } c) { _window.ChooseLanguage(c.Key); } };
        _menu.CheckedChanged += (_, _) => { if (!_filling && _menu.Checked != _window.MenuVisible) _window.ToggleMenu(); };
        _reset.Click += (_, _) => { _window.ResetLook(); Fill(); };
        _close.Click += (_, _) => Close();
        ActiveControl = _close;   // no combo box starts with its text selected
        Loc.Changed += ApplyTexts;
        ChronikologioTheme.Changed += Restyle;
        FormClosed += (_, _) => { Loc.Changed -= ApplyTexts; ChronikologioTheme.Changed -= Restyle; };
        ApplyTexts();
        Restyle();
    }

    private sealed record Choice(string Key, string Text)
    {
        public override string ToString() => Text;
    }

    private Panel AddCard(string heading, params (string Label, Control Control)[] rows)
    {
        var card = new Panel { Height = 40 + rows.Length * 46, Padding = new Padding(16, 12, 16, 8), Tag = "card" };
        var head = new Label { Text = heading, AutoSize = false, Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI", 11f, FontStyle.Bold) };
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = rows.Length };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < rows.Length; i++)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            var label = new Label { Text = rows[i].Label, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            _labels.Add(label);
            table.Controls.Add(label, 0, i);
            rows[i].Control.Anchor = AnchorStyles.Left;
            table.Controls.Add(rows[i].Control, 1, i);
        }
        card.Controls.Add(table);
        card.Controls.Add(head);
        _cards.Add((card, head));
        card.Margin = new Padding(0, 0, 0, 12);
        return card;
    }

    private Choice? Chosen(FlatComboBox box) =>
        _choices.TryGetValue(box, out var list) && box.SelectedIndex >= 0 && box.SelectedIndex < list.Count ? list[box.SelectedIndex] : null;

    private void Choices(FlatComboBox box, IEnumerable<Choice> choices, string current)
    {
        var list = choices.ToList();
        _choices[box] = list;
        var index = Math.Max(0, list.FindIndex(c => c.Key == current));
        box.SetItems(list.Select(c => c.Text), index);
    }
    private void Fill()
    {
        _filling = true;
        try
        {
            Choices(_theme, ChronikologioTheme.Names.Select(n => new Choice(n, ChronikologioTheme.DisplayName(n))), ChronikologioTheme.Name);
            Choices(_layout, UiSettings.LayoutNames.Select(n => new Choice(n, Loc.T("layout." + n))), _window.Layout);
            Choices(_region, Countries.All.Select(c => new Choice(c.Region, Loc.T("region." + c.Region))), _window.Region);
            Choices(_language, new[] { "auto", "el", "en" }.Select(c => new Choice(c, Loc.T("language." + c))), _window.LanguageSetting);
            _menu.Checked = _window.MenuVisible;
        }
        finally { _filling = false; }
    }

    private void ApplyTexts()
    {
        Text = Loc.T("settings.title");
        _cards[0].Head.Text = Loc.T("settings.group.look");
        _cards[1].Head.Text = Loc.T("settings.group.calendar");
        _labels[0].Text = Loc.T("settings.theme");
        _labels[1].Text = Loc.T("settings.layout");
        _labels[2].Text = "";
        _labels[3].Text = Loc.T("settings.region");
        _labels[4].Text = Loc.T("settings.language");
        _menu.Text = Loc.T("settings.menubar");
        _note.Text = Loc.T("settings.note");
        _reset.Text = Loc.T("settings.reset");
        _close.Text = Loc.T("settings.close");
        Fill();
    }

    private void Restyle()
    {
        var p = ChronikologioTheme.Palette;
        BackColor = p.Background;
        ForeColor = p.Text;
        foreach (var (card, head) in _cards) { card.BackColor = p.Surface; card.ForeColor = p.Text; head.BackColor = p.Surface; head.ForeColor = p.Accent; }
        foreach (var l in _labels) { l.BackColor = p.Surface; l.ForeColor = p.Text; }
        _menu.BackColor = p.Surface; _menu.ForeColor = p.Text;

        _note.BackColor = p.Background; _note.ForeColor = p.TextMuted;
        foreach (var table in _cards.SelectMany(c => c.Card.Controls.OfType<TableLayoutPanel>())) table.BackColor = p.Surface;
        Invalidate(true);
    }

    /// <summary>Opens the window over its owner and returns when it is closed.</summary>
    public static void ShowFor(MainWindow owner)
    {
        using var dialog = new SettingsDialog(owner);
        dialog.ShowDialog(owner);
    }

    // test helpers
    public IReadOnlyList<string> Themes => _choices[_theme].Select(c => c.Key).ToList();
    public void Pick(string which, string key)
    {
        FlatComboBox box = which switch { "theme" => _theme, "layout" => _layout, "region" => _region, _ => _language };
        box.SelectedIndex = _choices[box].FindIndex(c => c.Key == key);
    }
    public void Reset() => _reset.PerformClick();
}
