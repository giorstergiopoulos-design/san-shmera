using System.Runtime.InteropServices;
using Chronikologio.Core;

namespace Chronikologio.UI;

/// <summary>Dark (or light) scroll bars for scrollable controls, the way the other Opsis apps do it (the Windows "DarkMode_Explorer" visual style). Safe to call repeatedly.</summary>
public static class ThemeNative
{
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hwnd, string? appName, string? idList);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> Hooked = new();

    public static void StyleScrollbars(Control c)
    {
        void Set()
        {
            try { SetWindowTheme(c.Handle, ChronikologioTheme.IsDark ? "DarkMode_Explorer" : "Explorer", null); }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
        }
        if (c.IsHandleCreated) Set();
        if (Hooked.TryGetValue(c, out _)) return;
        Hooked.Add(c, new object());
        c.HandleCreated += (_, _) => Set();
    }
}

/// <summary>A vertical bar of icon buttons at the left edge of the window (today, the three layouts, search, theme, help). Drawn in the colours of the theme.</summary>
public sealed class SideRail : Control
{
    public sealed record RailItem(string Key, string Glyph, string Tip, Action Click);

    private readonly List<RailItem> _items = new();
    private readonly ToolTip _tip = new();
    private int _hover = -1;
    private string? _checked;

    public const int RailWidth = 52;

    public SideRail()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Dock = DockStyle.Left;
        Width = RailWidth;
        Cursor = Cursors.Hand;
    }

    public IReadOnlyList<RailItem> Items => _items;

    public void SetItems(IEnumerable<RailItem> items)
    {
        _items.Clear();
        _items.AddRange(items);
        Invalidate();
    }

    /// <summary>The key of the item drawn as selected (the layout in use), or null.</summary>
    public string? Checked
    {
        get => _checked;
        set { _checked = value; Invalidate(); }
    }

    private string? _pulse;

    /// <summary>Draws an item as pressed for a short moment.</summary>
    public void Pulse(string key)
    {
        _pulse = key;
        Invalidate();
        var timer = new System.Windows.Forms.Timer { Interval = 350 };
        timer.Tick += (_, _) => { timer.Stop(); timer.Dispose(); _pulse = null; Invalidate(); };
        timer.Start();
    }

    private Rectangle Cell(int index) => new(0, 8 + index * 48, Width, 44);

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ChronikologioTheme.Palette;
        var g = e.Graphics;
        g.Clear(p.Surface);
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using var glyphFont = new Font("Segoe MDL2 Assets", 15f);
        using var accent = new SolidBrush(p.Accent);
        using var hoverBrush = new SolidBrush(Color.FromArgb(40, p.Text));
        for (var i = 0; i < _items.Count; i++)
        {
            var r = Cell(i);
            var selected = _items[i].Key == _checked || _items[i].Key == _pulse;
            if (i == _hover || _items[i].Key == _pulse) g.FillRectangle(hoverBrush, r);
            if (selected) g.FillRectangle(accent, r.X, r.Y + 8, 3, r.Height - 16);
            if (_items[i].Key == "help") DrawInfo(g, r, selected ? p.Accent : p.TextMuted);
            else TextRenderer.DrawText(g, _items[i].Glyph, glyphFont, r, selected ? p.Accent : p.TextMuted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
        using var edge = new Pen(Color.FromArgb(60, p.Text));
        g.DrawLine(edge, Width - 1, 0, Width - 1, Height);
    }

    /// <summary>The icon of the About page of Opsis: a circle with a dot and a stem.</summary>
    private static void DrawInfo(Graphics g, Rectangle cell, Color color)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var pen = new Pen(color, 1.8f) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        using var brush = new SolidBrush(color);
        var cx = cell.X + cell.Width / 2f; var cy = cell.Y + cell.Height / 2f; const float r = 10f;
        g.DrawEllipse(pen, cx - r, cy - r, 2 * r, 2 * r);
        g.FillEllipse(brush, cx - 1.4f, cy - 5.6f, 2.8f, 2.8f);
        g.DrawLine(pen, cx, cy - 1.2f, cx, cy + 5.5f);
        g.SmoothingMode = old;
    }

    private int IndexAt(Point point)
    {
        for (var i = 0; i < _items.Count; i++) if (Cell(i).Contains(point)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var i = IndexAt(e.Location);
        if (i == _hover) return;
        _hover = i;
        if (i >= 0) _tip.Show(_items[i].Tip, this, Width + 4, Cell(i).Y + 10, 2000); else _tip.Hide(this);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1;
        _tip.Hide(this);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        var i = IndexAt(e.Location);
        if (i >= 0) _items[i].Click();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Tabs that follow the theme: a header strip drawn with the palette (the system tab control cannot be made dark) above the page of the chosen tab.</summary>
public sealed class TabHost : Panel
{
    private sealed class Header : Control
    {
        private readonly TabHost _owner;
        private int _hover = -1;

        public Header(TabHost owner)
        {
            _owner = owner;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Dock = DockStyle.Top;
            Height = 36;
            Cursor = Cursors.Hand;
        }

        private Rectangle Cell(int i) => new(i * 150, 0, 150, Height);

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = ChronikologioTheme.Palette;
            e.Graphics.Clear(p.Background);
            for (var i = 0; i < _owner._pages.Count; i++)
            {
                var r = Cell(i);
                var selected = i == _owner.SelectedIndex;
                if (selected)
                {
                    using var back = new SolidBrush(p.Surface);
                    e.Graphics.FillRectangle(back, r);
                    using var line = new SolidBrush(p.Accent);
                    e.Graphics.FillRectangle(line, r.X, r.Bottom - 3, r.Width, 3);
                }
                else if (i == _hover)
                {
                    using var back = new SolidBrush(Color.FromArgb(30, p.Text));
                    e.Graphics.FillRectangle(back, r);
                }
                TextRenderer.DrawText(e.Graphics, _owner._pages[i].Text, Font, r, selected ? p.Accent : p.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var i = e.X / 150;
            if (i >= _owner._pages.Count) i = -1;
            if (i != _hover) { _hover = i; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover = -1; Invalidate(); }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            var i = e.X / 150;
            if (e.Button == MouseButtons.Left && i < _owner._pages.Count) _owner.SelectedIndex = i;
        }
    }

    private readonly List<Control> _pages = new();
    private readonly Header _header;
    private readonly Panel _body = new() { Dock = DockStyle.Fill };
    private int _selected = -1;

    public event Action? SelectedChanged;

    public TabHost()
    {
        _header = new Header(this);
        Controls.Add(_body);
        Controls.Add(_header);
    }

    /// <summary>Adds pages; the Text of each page is the title of its tab.</summary>
    public void AddPages(params Control[] pages)
    {
        foreach (var page in pages)
        {
            page.Dock = DockStyle.Fill;
            page.Visible = false;
            _pages.Add(page);
            _body.Controls.Add(page);
        }
        if (_selected < 0 && _pages.Count > 0) SelectedIndex = 0;
        _header.Invalidate();
    }

    public void RefreshTitles() => _header.Invalidate();

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (value < 0 || value >= _pages.Count || value == _selected) return;
            _selected = value;
            for (var i = 0; i < _pages.Count; i++) _pages[i].Visible = i == _selected;
            _header.Invalidate();
            SelectedChanged?.Invoke();
        }
    }

    public Control? Selected
    {
        get => _selected >= 0 ? _pages[_selected] : null;
        set { var i = value is null ? -1 : _pages.IndexOf(value); if (i >= 0) SelectedIndex = i; }
    }

    public void Restyle()
    {
        var p = ChronikologioTheme.Palette;
        BackColor = p.Background;
        _body.BackColor = p.Background;
        foreach (var page in _pages) page.BackColor = p.Background;
        _header.Invalidate();
    }
}
