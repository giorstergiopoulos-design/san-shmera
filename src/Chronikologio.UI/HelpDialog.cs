using Chronikologio.Core;

namespace Chronikologio.UI;

/// <summary>
/// The Help window: tabs for the instructions, the version history, the licence and the sources, each page a stack of cards in the colours of the current theme
/// (the same arrangement as the help of the other Opsis apps).
/// </summary>
public sealed class HelpDialog : Form
{
    /// <summary>A page: a scrolling stack of cards, each with a heading and a text.</summary>
    public sealed class CardPage : Panel
    {
        private readonly List<(Label Head, Label Body, Panel Card)> _cards = new();
        private const int Gap = 12, Pad = 16;

        public CardPage(string title)
        {
            Text = title;
            Dock = DockStyle.Fill;
            AutoScroll = true;
            Resize += (_, _) => Arrange();
        }

        public int CardCount => _cards.Count;

        public void Add(string heading, string body)
        {
            var card = new Panel { Tag = "card" };
            var head = new Label { Text = heading, AutoSize = false, Font = new Font("Segoe UI", 11f, FontStyle.Bold) };
            var text = new Label { Text = body, AutoSize = false, Font = new Font("Segoe UI", 10f) };
            card.Controls.Add(head);
            card.Controls.Add(text);
            Controls.Add(card);
            _cards.Add((head, text, card));
        }

        public void Restyle()
        {
            var p = ChronikologioTheme.Palette;
            BackColor = p.Background;
            foreach (var (head, body, card) in _cards)
            {
                card.BackColor = p.Surface;
                head.BackColor = p.Surface; head.ForeColor = p.Accent;
                body.BackColor = p.Surface; body.ForeColor = p.Text;
            }
            Arrange();
        }

        /// <summary>Lays the cards out under each other, as wide as the page, each as tall as its text.</summary>
        public void Arrange()
        {
            if (_cards.Count == 0) return;
            var width = Math.Max(240, ClientSize.Width - 2 * Gap - SystemInformation.VerticalScrollBarWidth);
            var y = Gap;
            SuspendLayout();
            foreach (var (head, body, card) in _cards)
            {
                var inner = width - 2 * Pad;
                var hh = TextRenderer.MeasureText(head.Text, head.Font, new Size(inner, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + 4;
                var bh = TextRenderer.MeasureText(body.Text, body.Font, new Size(inner, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + 6;
                head.SetBounds(Pad, Pad - 4, inner, hh);
                body.SetBounds(Pad, Pad - 4 + hh + 6, inner, bh);
                card.SetBounds(Gap, y, width, Pad - 4 + hh + 6 + bh + Pad);
                y += card.Height + Gap;
            }
            AutoScrollMinSize = new Size(0, y);
            ResumeLayout();
        }
    }

    private readonly TabHost _tabs = new() { Dock = DockStyle.Fill };
    private readonly CardPage _guide, _changelog, _license, _sources;

    public HelpDialog()
    {
        Text = Loc.T("help.title");
        Size = new Size(860, 680);
        MinimumSize = new Size(560, 420);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        KeyPreview = true;
        Font = new Font("Segoe UI", 10f);
        KeyDown += (_, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        _guide = new CardPage(Loc.T("help.tab.guide"));
        _changelog = new CardPage(Loc.T("help.tab.changelog"));
        _license = new CardPage(Loc.T("help.tab.license"));
        _sources = new CardPage(Loc.T("help.tab.sources"));
        FillGuide();
        FillChangelog();
        _license.Add(Loc.T("help.license.summary"), License.Summary);
        _license.Add(Loc.T("help.license.disclaimer"), License.Disclaimer);
        _license.Add(Loc.T("help.license.content"), License.Content.Substring(License.Content.IndexOf('\n') + 1));
        _license.Add(Loc.T("help.license.terms"), License.Mit);

        _tabs.AddPages(_guide, _changelog, _license, _sources);
        Controls.Add(_tabs);
        Restyle();
        ChronikologioTheme.Changed += Restyle;
        FormClosed += (_, _) => ChronikologioTheme.Changed -= Restyle;
    }

    public int SelectedTab
    {
        get => _tabs.SelectedIndex;
        set => _tabs.SelectedIndex = Math.Clamp(value, 0, 3);
    }

    public IReadOnlyList<CardPage> Pages => new[] { _guide, _changelog, _license, _sources };

    /// <summary>The guide is one text with headings in capitals; every heading becomes a card. The sources get a tab of their own.</summary>
    private void FillGuide()
    {
        foreach (var block in Loc.T("guide.text").Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            var nl = block.IndexOf('\n');
            var heading = nl > 0 ? block[..nl].Trim() : block.Trim();
            var body = nl > 0 ? block[(nl + 1)..].Trim() : "";
            var page = heading.StartsWith("ΠΗΓΕΣ", StringComparison.Ordinal) ? _sources : _guide;
            page.Add(Capitalise(heading), body);
        }
        _sources.Add(Loc.T("help.sources.data"), Loc.T("help.sources.data.text"));
    }

    private static readonly Dictionary<string, string> Headings = new()
    {
        ["ΤΙ ΕΙΝΑΙ"] = "Τι είναι", ["ΧΡΗΣΗ"] = "Χρήση", ["ΕΜΦΑΝΙΣΗ"] = "Εμφάνιση", ["ΠΡΟΣΟΧΗ ΣΤΑ ΔΕΔΟΜΕΝΑ"] = "Προσοχή στα δεδομένα",
        ["ΠΗΓΕΣ ΚΑΙ ΕΥΧΑΡΙΣΤΙΕΣ"] = "Πηγές και ευχαριστίες", ["ΜΕ ΤΟ OPSIS"] = "Με το Opsis"
    };

    /// <summary>The headings of the guide are written in capitals (without accents); the card shows the usual spelling.</summary>
    private static string Capitalise(string s) => Headings.TryGetValue(s, out var h) ? h : s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..].ToLowerInvariant();

    private void FillChangelog()
    {
        foreach (var entry in Changelog.Entries)
            _changelog.Add($"{entry.Version}  ·  {entry.Date}", string.Join("\n", entry.Items.Select(i => "• " + i)));
    }

    private void Restyle()
    {
        var p = ChronikologioTheme.Palette;
        BackColor = p.Background;
        ForeColor = p.Text;
        foreach (var page in Pages)
        {
            page.Restyle();
            ThemeNative.StyleScrollbars(page);
        }
        _tabs.Restyle();
        if (IsHandleCreated) TitleBar.Apply(Handle, p.IsDark);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        TitleBar.Apply(Handle, ChronikologioTheme.Palette.IsDark);
    }

    public static void ShowFor(IWin32Window? owner, int tab)
    {
        using var dialog = new HelpDialog { SelectedTab = tab };
        dialog.ShowDialog(owner);
    }
}
