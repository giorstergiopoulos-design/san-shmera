using System.Globalization;
using Chronikologio.Core;

namespace Chronikologio.UI;

/// <summary>
/// The search of the window, at its top left corner (the same way as in GearWin): a magnifier, a box, and while one types a list under the box with
/// the names, saints and events that match; choosing one (click, or arrows and Enter) takes the window to that day.
/// </summary>
public sealed class SearchBar : Panel
{
    private sealed record Entry(string Folded, string Text, string Kind, int Month, int Day, int Priority = 0);

    private sealed class Magnifier : Control
    {
        public Magnifier() { SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true); Size = new Size(36, 38); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var pen = new Pen(ChronikologioTheme.Palette.TextMuted, 2f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
            var cx = Width / 2f - 2; var cy = Height / 2f - 2;
            g.DrawEllipse(pen, cx - 6, cy - 6, 12, 12);
            g.DrawLine(pen, cx + 4.5f, cy + 4.5f, cx + 9, cy + 9);
        }
    }

    private readonly TextBox _box = new() { BorderStyle = BorderStyle.FixedSingle, Width = 240 };
    private readonly Magnifier _icon = new();
    private readonly ListBox _list = new() { Visible = false, BorderStyle = BorderStyle.FixedSingle, IntegralHeight = false, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 28, TabStop = false };
    private readonly System.Windows.Forms.Timer _hide = new() { Interval = 180 };
    private DayStore? _store;
    private List<Entry>? _index;
    private readonly List<Entry> _shown = new();

    /// <summary>Raised when a result is chosen: the day to go to (in the current year).</summary>
    public event Action<DateOnly>? DateChosen;

    public SearchBar()
    {
        Dock = DockStyle.Right;
        Width = 290;
        Height = 44;
        Controls.Add(_icon);
        Controls.Add(_box);
        Resize += (_, _) => Arrange();
        Arrange();

        _box.TextChanged += (_, _) => Update_();
        _box.KeyDown += OnKeyDown;
        _box.Leave += (_, _) => { _hide.Stop(); _hide.Start(); };
        _box.Enter += (_, _) => { if (_box.Text.Trim().Length >= 2) Update_(); };
        _hide.Tick += (_, _) => { _hide.Stop(); if (!_box.Focused && !_list.Focused) _list.Visible = false; };
        _list.DrawItem += DrawItem;
        _list.MouseDown += (_, e) =>
        {
            var i = _list.IndexFromPoint(e.Location);
            if (i >= 0) Choose(i);
        };
        HandleCreated += (_, _) => AttachList();
    }

    private void Arrange()
    {
        _icon.Location = new Point(4, Math.Max(0, (Height - _icon.Height) / 2));
        _box.Location = new Point(_icon.Right, Math.Max(0, (Height - _box.Height) / 2));
    }

    private void AttachList()
    {
        var form = FindForm();
        if (form is null || _list.Parent == form) return;
        form.Controls.Add(_list);
    }

    /// <summary>The texts of the results shown now (for the tests).</summary>
    public IReadOnlyList<string> Results => _shown.Select(s => s.Text).ToList();

    /// <summary>Types a query as a person would.</summary>
    public void Search(string query) => _box.Text = query;

    /// <summary>Chooses a result as a click would.</summary>
    public void ChooseResult(int index) => Choose(index);

    public bool ListVisible => _list.Visible;

    public void SetPlaceholder(string text) => _box.PlaceholderText = text;

    public void SetStore(DayStore store)
    {
        _store = store;
        _index = null;
        Clear();
    }

    public void Clear()
    {
        _box.Clear();
        _list.Visible = false;
    }

    public new void Focus() => _box.Focus();

    public void Restyle()
    {
        var p = ChronikologioTheme.Palette;
        BackColor = Color.Transparent;   // the waves of the header show through
        _icon.BackColor = Color.Transparent;
        _icon.Invalidate();
        _box.BackColor = p.Surface;
        _box.ForeColor = p.Text;
        _list.BackColor = p.Surface;
        _list.ForeColor = p.Text;
        ThemeNative.StyleScrollbars(_box);
        ThemeNative.StyleScrollbars(_list);
        _list.Invalidate();
    }

    private List<Entry> Index()
    {
        if (_index is not null || _store is null) return _index ?? new List<Entry>();
        var list = new List<Entry>();
        for (var i = 0; i < 366; i++)
        {
            var d = new DateOnly(2024, 1, 1).AddDays(i);
            var day = _store.GetDay(d.Month, d.Day);
            foreach (var c in day.Commemorations)
            {
                // the memories taken from the Megas Synaxaristis come after the hand-written ones in the results
                var priority = c.Sources.Any(s => s.Url?.Contains("synaxarion.gr/gr/sid/", StringComparison.Ordinal) == true) ? 1 : 0;
                foreach (var n in c.Names) list.Add(new Entry(TextNorm.Fold(n.Name + " " + string.Join(" ", n.Variants)), n.Name, "name", d.Month, d.Day, priority));
                list.Add(new Entry(TextNorm.Fold(c.Title), c.Title, "saint", d.Month, d.Day, priority));
            }
            foreach (var e in day.Events)
                list.Add(new Entry(TextNorm.Fold(e.Title), (e.Year is { } y ? y.ToString(CultureInfo.InvariantCulture) + " · " : "") + e.Title, "event", d.Month, d.Day));
        }
        return _index = list;
    }

    private void Update_()
    {
        var q = TextNorm.Fold(_box.Text.Trim());
        _shown.Clear();
        _list.Items.Clear();
        if (q.Length < 2) { _list.Visible = false; return; }
        var hits = Index().Where(e => e.Folded.Contains(q, StringComparison.Ordinal))
            .OrderBy(e => e.Kind == "name" ? 0 : e.Kind == "saint" ? 1 : 2)
            .ThenBy(e => e.Folded.StartsWith(q, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(e => e.Priority)
            .DistinctBy(e => (e.Text, e.Month, e.Day)).ToList();
        // a few of each kind, so that a name celebrated on many days does not push the saints and the events out of the list
        hits = hits.Where(e => e.Kind == "name").Take(6).Concat(hits.Where(e => e.Kind == "saint").Take(6)).Concat(hits.Where(e => e.Kind == "event").Take(4)).ToList();
        if (hits.Count == 0)
        {
            _list.Items.Add(Loc.T("search.none"));
        }
        else
        {
            _shown.AddRange(hits);
            foreach (var h in hits) _list.Items.Add(h.Text);
        }
        var rows = Math.Min(Math.Max(_list.Items.Count, 1), 12);
        _list.Size = new Size(Math.Max(_box.Width, 420), rows * _list.ItemHeight + 4);
        var form = FindForm();
        if (form is null) return;
        AttachList();
        var at = form.PointToClient(_box.PointToScreen(new Point(0, _box.Height + 2)));
        // the box is at the right of the window: keep the list inside it
        _list.Location = new Point(Math.Max(0, Math.Min(at.X, form.ClientSize.Width - _list.Width - 8)), at.Y);
        _list.Visible = true;
        _list.BringToFront();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) { _list.Visible = false; e.SuppressKeyPress = true; return; }
        if (!_list.Visible || _shown.Count == 0) return;
        if (e.KeyCode == Keys.Down) { _list.SelectedIndex = Math.Min(_list.SelectedIndex + 1, _shown.Count - 1); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Up) { _list.SelectedIndex = Math.Max(_list.SelectedIndex - 1, 0); e.SuppressKeyPress = true; }
        else if (e.KeyCode == Keys.Enter) { Choose(_list.SelectedIndex >= 0 ? _list.SelectedIndex : 0); e.SuppressKeyPress = true; }
    }

    private void Choose(int index)
    {
        if (index < 0 || index >= _shown.Count) return;
        var h = _shown[index];
        var year = DateTime.Today.Year;
        _list.Visible = false;
        DateChosen?.Invoke(new DateOnly(year, h.Month, Math.Min(h.Day, DateTime.DaysInMonth(year, h.Month))));
    }

    private void DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var p = ChronikologioTheme.Palette;
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var back = new SolidBrush(selected ? Color.FromArgb(50, p.Accent) : p.Surface);
        e.Graphics.FillRectangle(back, e.Bounds);
        var text = _list.Items[e.Index]?.ToString() ?? "";
        var hit = e.Index < _shown.Count ? _shown[e.Index] : null;
        var left = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 130, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, text, Font, left, p.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        if (hit is null) return;
        var date = new DateTime(2000, hit.Month, hit.Day).ToString("d MMM", Loc.Culture);
        TextRenderer.DrawText(e.Graphics, date, Font, new Rectangle(e.Bounds.Right - 120, e.Bounds.Y, 112, e.Bounds.Height), p.TextMuted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Right | TextFormatFlags.NoPadding);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _hide.Dispose(); _list.Dispose(); }
        base.Dispose(disposing);
    }
}
