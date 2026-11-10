using System.Globalization;

namespace Chronikologio.UI;

/// <summary>Month grid (Monday first). A dot under a day means the database has something for it.</summary>
public sealed class CalendarGrid : Control
{
    /// <summary>Two-letter day names of the interface language, Monday first.</summary>
    private static string[] Heads
    {
        get
        {
            var names = Chronikologio.Core.Loc.Culture.DateTimeFormat.AbbreviatedDayNames;   // Sunday first
            return Enumerable.Range(1, 7).Select(i => names[i % 7].TrimEnd('.')).Select(n => n.Length > 2 ? n[..2] : n).ToArray();
        }
    }

    private DateOnly _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateOnly _selected = DateOnly.FromDateTime(DateTime.Today);

    public Func<DateOnly, bool> HasData { get; set; } = _ => false;

    /// <summary>Days of a great feast (Easter, the Dormition...) are marked with a ring; every day has data now, so the dot alone says little.</summary>
    public Func<DateOnly, bool> IsMajor { get; set; } = _ => false;

    /// <summary>Days off (public holidays and national feast days) are drawn in red.</summary>
    public Func<DateOnly, bool> IsDayOff { get; set; } = _ => false;
    public Color Accent { get; set; } = Color.FromArgb(0, 150, 200);
    public Color Muted { get; set; } = Color.Gray;
    public event Action<DateOnly>? SelectedChanged;

    public CalendarGrid() => SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

    public DateOnly Month
    {
        get => _month;
        set { _month = new DateOnly(value.Year, value.Month, 1); Invalidate(); }
    }

    public DateOnly Selected
    {
        get => _selected;
        set
        {
            if (value == _selected) return;
            _selected = value;
            if (value.Year != _month.Year || value.Month != _month.Month) _month = new DateOnly(value.Year, value.Month, 1);
            Invalidate();
            SelectedChanged?.Invoke(value);
        }
    }

    private int LeadingBlanks => ((int)_month.DayOfWeek + 6) % 7;

    private RectangleF Cell(int index, float headH)
    {
        var w = Width / 7f;
        var h = (Height - headH) / 6f;
        return new RectangleF(index % 7 * w, headH + index / 7 * h, w, h);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        var headH = Font.Height + 6f;
        using var muted = new SolidBrush(Muted);
        using var fore = new SolidBrush(ForeColor);
        using var accent = new SolidBrush(Accent);
        using var onAccent = new SolidBrush(DayLayout.OnAccent(Accent));

        var heads = Heads;
        for (var i = 0; i < 7; i++)
            g.DrawString(heads[i], Font, muted, new RectangleF(i * Width / 7f, 0, Width / 7f, headH), sf);

        var days = DateTime.DaysInMonth(_month.Year, _month.Month);
        var today = DateOnly.FromDateTime(DateTime.Today);
        for (var d = 1; d <= days; d++)
        {
            var cell = Cell(LeadingBlanks + d - 1, headH);
            // The number, its circle and its dot form one group, centred in the cell: in a tall cell (the small calendar of the layouts) they stay together
            // instead of the circle sitting at the top of the cell, the number in the middle and the dot at the bottom.
            var eh = Math.Min(cell.Height, Math.Max(cell.Width * 1.35f, Font.Height * 2.4f));
            var r = new RectangleF(cell.X, cell.Y + (cell.Height - eh) / 2, cell.Width, eh);
            var tr = new RectangleF(r.X, r.Y, r.Width, r.Height - 6f);   // where the number is, above the dot
            var date = new DateOnly(_month.Year, _month.Month, d);
            var selected = date == _selected;
            var dia = Math.Max(12f, Math.Min(r.Width - 8f, tr.Height - 4f));
            var ring = new RectangleF(tr.X + (tr.Width - dia) / 2, tr.Y + (tr.Height - dia) / 2, dia, dia);
            if (selected) g.FillEllipse(accent, ring);
            else if (date == today)
            {
                using var pen = new Pen(Accent, 1.5f);
                g.DrawEllipse(pen, ring);
            }
            var major = IsMajor(date);
            using var bold = major ? new Font(Font, FontStyle.Bold) : null;
            var off = IsDayOff(date);
            using var red = new SolidBrush(Color.FromArgb(226, 75, 74));
            g.DrawString(d.ToString(CultureInfo.InvariantCulture), bold ?? Font, selected ? onAccent : off ? red : major ? accent : fore, tr, sf);
            if (HasData(date) && !selected)
            {
                // A dot for an ordinary day, a longer bar for a great feast.
                if (major) g.FillRectangle(accent, r.X + r.Width / 2 - 7, r.Bottom - 7, 14, 3);
                else g.FillEllipse(muted, r.X + r.Width / 2 - 2, r.Bottom - 7, 4, 4);
            }
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        var headH = Font.Height + 6f;
        if (e.Y < headH) return;
        var col = (int)(e.X / (Width / 7f));
        var row = (int)((e.Y - headH) / ((Height - headH) / 6f));
        var day = row * 7 + col - LeadingBlanks + 1;
        if (day >= 1 && day <= DateTime.DaysInMonth(_month.Year, _month.Month))
            Selected = new DateOnly(_month.Year, _month.Month, day);
    }
}
