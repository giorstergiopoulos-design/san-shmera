using System.Drawing.Drawing2D;
using Chronikologio.Core;

namespace Chronikologio.UI;

/// <summary>
/// A band with a slowly moving soft background in the colours of the theme (the same kind of living background as in Opsis): a few translucent light blobs drift
/// over the surface. It is cheap (about 12 pictures a second) and stops completely when the band is not visible or the window is minimised.
/// Controls placed on it with a transparent background are drawn over the movement.
/// </summary>
public sealed class AuroraBand : Control
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 80 };
    private double _t;

    public AuroraBand()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        _timer.Tick += (_, _) =>
        {
            if (!Visible || !IsHandleCreated || FindForm() is not { WindowState: not FormWindowState.Minimized } || !Parent!.Visible) return;
            _t += 0.035;
            Invalidate();
        };
        _timer.Start();
    }

    /// <summary>Turns the movement off (the background then stays still).</summary>
    public bool Animated
    {
        get => _timer.Enabled;
        set => _timer.Enabled = value;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ChronikologioTheme.Palette;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(p.Surface);
        if (Width < 20 || Height < 10) return;
        var clip = g.Clip;
        using var round = Rounded(new RectangleF(0, 0, Width - 1, Height - 1), 10f);
        g.SetClip(round, CombineMode.Intersect);
        // three blobs that drift in slow circles: accent, a lighter accent and the text colour (almost invisible)
        var blobs = new (double phase, double speed, Color color, float size)[]
        {
            (0.0, 1.0, Color.FromArgb(p.IsDark ? 70 : 60, p.Accent), 0.9f),
            (2.1, 0.7, Color.FromArgb(p.IsDark ? 45 : 40, ControlPaint.Light(p.Accent, 0.5f)), 0.7f),
            (4.2, 0.5, Color.FromArgb(p.IsDark ? 28 : 24, p.Text), 0.6f)
        };
        foreach (var (phase, speed, color, size) in blobs)
        {
            var cx = (float)(Width * (0.5 + 0.42 * Math.Sin(_t * speed + phase)));
            var cy = (float)(Height * (0.5 + 0.3 * Math.Cos(_t * speed * 0.8 + phase * 1.3)));
            var d = Math.Max(Width, Height * 3) * size * 0.45f;
            using var path = new GraphicsPath();
            path.AddEllipse(cx - d / 2, cy - d / 2, d, d);
            using var brush = new PathGradientBrush(path) { CenterColor = color, SurroundColors = new[] { Color.FromArgb(0, color) } };
            g.FillPath(brush, path);
        }
        g.Clip = clip;
        using var pen = new Pen(Color.FromArgb(60, p.Text), 1f);
        g.DrawPath(pen, round);
    }

    private static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _timer.Stop(); _timer.Dispose(); }
        base.Dispose(disposing);
    }
}

/// <summary>
/// A time strip: a horizontal line from the antiquity to today with a dot for every event, birth and death of the day, at its year (the scale is stretched on the
/// recent centuries, where most things happen). A tooltip names the dot, a click chooses it.
/// </summary>
public sealed class YearTimeline : Control
{
    public sealed record Mark(int Year, string Text, int Kind, int Index);

    private readonly List<Mark> _marks = new();
    private readonly ToolTip _tip = new();
    private int _hover = -1;

    // years where the scale changes: the antiquity is squeezed, the 19th and 20th centuries are wide
    private static readonly (int Year, double At)[] Anchors = { (-1500, 0.0), (0, 0.16), (1000, 0.30), (1500, 0.43), (1800, 0.62), (1900, 0.8), (2030, 1.0) };

    public event Action<Mark>? MarkClicked;

    public YearTimeline()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Height = 66;
        Cursor = Cursors.Hand;
    }

    public void SetMarks(IEnumerable<Mark> marks)
    {
        _marks.Clear();
        _marks.AddRange(marks.OrderBy(m => m.Year));
        _hover = -1;
        Invalidate();
    }

    public IReadOnlyList<Mark> Marks => _marks;

    private static double Position(int year)
    {
        year = Math.Clamp(year, Anchors[0].Year, Anchors[^1].Year);
        for (var i = 1; i < Anchors.Length; i++)
            if (year <= Anchors[i].Year)
                return Anchors[i - 1].At + (Anchors[i].At - Anchors[i - 1].At) * (year - Anchors[i - 1].Year) / (double)(Anchors[i].Year - Anchors[i - 1].Year);
        return 1.0;
    }

    private PointF PointOf(Mark m, int index)
    {
        const int pad = 22;
        var x = pad + (float)(Position(m.Year) * (Width - 2 * pad));
        // marks of the same period do not sit on each other: they step up and down
        var row = _marks.Take(index).Count(o => Math.Abs(PointOf0(o) - x) < 11) % 3;
        return new PointF(x, Height / 2f - 14 + row * 11);
    }

    private float PointOf0(Mark m) => 22 + (float)(Position(m.Year) * (Width - 44));

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ChronikologioTheme.Palette;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(p.Background);
        var y = Height - 24f;
        using var line = new Pen(Color.FromArgb(120, p.TextMuted), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        g.DrawLine(line, 14, y, Width - 14, y);
        using var small = new Font("Segoe UI", 8f);
        foreach (var tick in new[] { -1000, 0, 500, 1000, 1500, 1800, 1900, 2000 })
        {
            var x = 22 + (float)(Position(tick) * (Width - 44));
            using var tickPen = new Pen(Color.FromArgb(110, p.TextMuted), 1f);
            g.DrawLine(tickPen, x, y - 3, x, y + 3);
            var label = tick < 0 ? $"{-tick} π.Χ." : tick.ToString();
            TextRenderer.DrawText(g, label, small, new Rectangle((int)x - 28, (int)y + 4, 56, 14), p.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
        }
        for (var i = 0; i < _marks.Count; i++)
        {
            var m = _marks[i];
            var pt = PointOf(m, i);
            var color = m.Kind switch { 0 => p.Accent, 1 => Color.FromArgb(226, 180, 74), 2 => Color.FromArgb(120, 200, 130), _ => Color.FromArgb(200, 110, 110) };
            var r = i == _hover ? 6.5f : 4.5f;
            using var stem = new Pen(Color.FromArgb(80, color), 1f);
            g.DrawLine(stem, pt.X, pt.Y, pt.X, y);
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, pt.X - r, pt.Y - r, 2 * r, 2 * r);
        }
        if (_marks.Count == 0)
            TextRenderer.DrawText(g, Loc.T("timeline.none"), Font, new Rectangle(0, 0, Width, (int)y), p.TextMuted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private int HitTest(Point at)
    {
        var best = -1; var bestD = 12.0;
        for (var i = 0; i < _marks.Count; i++)
        {
            var pt = PointOf(_marks[i], i);
            var d = Math.Sqrt((pt.X - at.X) * (pt.X - at.X) + (pt.Y - at.Y) * (pt.Y - at.Y));
            if (d < bestD) { best = i; bestD = d; }
        }
        return best;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var i = HitTest(e.Location);
        if (i == _hover) return;
        _hover = i;
        if (i >= 0) _tip.Show(MarkText(_marks[i]), this, e.X + 12, e.Y + 18, 2500); else _tip.Hide(this);
        Invalidate();
    }

    private static string MarkText(Mark m) => (m.Year < 0 ? $"{-m.Year} π.Χ." : m.Year.ToString()) + " — " + m.Text;

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = -1; _tip.Hide(this); Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        var i = HitTest(e.Location);
        if (i >= 0) MarkClicked?.Invoke(_marks[i]);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tip.Dispose();
        base.Dispose(disposing);
    }
}
