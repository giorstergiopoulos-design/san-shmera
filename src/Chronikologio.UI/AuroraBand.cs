using System.Drawing.Drawing2D;

using Chronikologio.Core;

namespace Chronikologio.UI;

/// <summary>One clock for every living background of the window (about 12 pictures a second); it runs only while something listens.</summary>
public static class WaveClock
{
    private static readonly System.Windows.Forms.Timer Timer = new() { Interval = 80 };
    private static int _listeners;
    private static event Action? TickEvent;

    public static double Time { get; private set; }

    static WaveClock()
    {
        Timer.Tick += (_, _) => { Time += 0.03; TickEvent?.Invoke(); };
    }

    public static void Subscribe(Action handler)
    {
        TickEvent += handler;
        if (++_listeners == 1) Timer.Start();
    }

    public static void Unsubscribe(Action handler)
    {
        TickEvent -= handler;
        if (--_listeners <= 0) { _listeners = 0; Timer.Stop(); }
    }

    /// <summary>True when a control should draw the next frame: it is shown and its window is not minimised.</summary>
    public static bool ShouldDraw(Control c) => c.IsHandleCreated && c.Visible && c.FindForm() is { WindowState: not FormWindowState.Minimized };
}

/// <summary>
/// The living background of Opsis: a few thin, translucent lines that flow slowly across the band like waves. Drawn behind the title of every screen and in the bands.
/// </summary>
public static class Waves
{
    public static void Draw(Graphics g, Rectangle area, double time, Opsis.UI.OpsisPalette palette)
    {
        if (area.Width < 20 || area.Height < 8) return;
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        // line: wavelength (as a part of the width), amplitude (as a part of the height), speed, where it sits in the band, opacity
        var lines = new (double length, double amp, double speed, double at, int alpha, bool accent)[]
        {
            (0.9, 0.16, 0.55, 0.42, 120, true),
            (0.6, 0.12, -0.8, 0.52, 90, false),
            (1.3, 0.22, 0.35, 0.48, 70, true),
            (0.45, 0.08, 1.1, 0.58, 50, false)
        };
        var color = palette.IsDark ? Color.FromArgb(150, 190, 230) : Color.FromArgb(90, 150, 200);
        foreach (var (length, amp, speed, at, alpha, accent) in lines)
        {
            var points = new List<PointF>();
            var step = Math.Max(6, area.Width / 120);
            for (var x = 0; x <= area.Width; x += step)
            {
                var phase = x / (area.Width * length) * Math.PI * 2 + time * speed * 2.2;
                var y = area.Y + area.Height * at + Math.Sin(phase) * area.Height * amp + Math.Sin(phase * 0.5 + time * 0.4) * area.Height * amp * 0.5;
                points.Add(new PointF(area.X + x, (float)y));
            }
            using var pen = new Pen(Color.FromArgb(alpha, accent ? palette.Accent : color), 1.2f);
            g.DrawCurve(pen, points.ToArray(), 0.4f);
        }
        g.SmoothingMode = old;
    }
}

/// <summary>A band with the waves behind it (used for the description of the categories). Controls on it with a transparent background are drawn over the movement.</summary>
public sealed class AuroraBand : Control
{
    private readonly Action _tick;

    public AuroraBand()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        _tick = () => { if (Animated && WaveClock.ShouldDraw(this) && Parent is { Visible: true }) Invalidate(); };
        WaveClock.Subscribe(_tick);
    }

    /// <summary>Turns the movement off (the background then stays still).</summary>
    public bool Animated { get; set; } = true;

    protected override void OnPaint(PaintEventArgs e)
    {
        var p = ChronikologioTheme.Palette;
        var g = e.Graphics;
        g.Clear(p.Surface);
        Waves.Draw(g, new Rectangle(0, 0, Width, Height), WaveClock.Time, p);
        using var pen = new Pen(Color.FromArgb(60, p.Text), 1f);
        g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) WaveClock.Unsubscribe(_tick);
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
