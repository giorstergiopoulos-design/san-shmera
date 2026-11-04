using Chronikologio.Core;
using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

/// <summary>
/// Automatic look-and-feel audit of the main window: every layout, both themes, several window sizes and busy days are built and the control tree is checked
/// for what a person would see as a defect (a control outside its parent, controls on top of each other, text cut off, unreadable colours, a blank window).
/// </summary>
[Collection("theme")]
public class UiAuditTests : IDisposable
{
    private readonly string _settings = Path.Combine(Path.GetTempPath(), "chronikologio-audit-" + Guid.NewGuid().ToString("N") + ".json");
    private static string DataRoot() => Path.Combine(AppContext.BaseDirectory, "data");

    public void Dispose()
    {
        if (File.Exists(_settings)) File.Delete(_settings);
        Loc.Set("el");
        ChronikologioTheme.Set("dark");
    }

    public static IEnumerable<object[]> Sizes() => new[]
    {
        new object[] { 900, 620 }, new object[] { 1100, 700 }, new object[] { 1366, 768 }, new object[] { 1920, 1080 },
    };

    /// <summary>The window really shown (invisible and out of the way), because the controls of a window that was never shown are not laid out.</summary>
    private MainWindow Open(int width, int height)
    {
        var window = new MainWindow(DataRoot(), _settings) { StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000), ShowInTaskbar = false, Opacity = 0 };
        window.ClientSize = new Size(width, height);
        window.Show();
        Application.DoEvents();
        return window;
    }

    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control c in root.Controls)
        {
            yield return c;
            foreach (var d in All(c)) yield return d;
        }
    }

    private static bool Shown(Control c)
    {
        for (var p = c; p is not null && p is not Form; p = p.Parent) if (!p.Visible) return false;
        return true;
    }

    private static string Short(string s) => (s.Length > 24 ? s[..24] + "…" : s).Replace("\r", " ").Replace("\n", " ");
    private static string Name(Control c) => $"{c.GetType().Name}{(string.IsNullOrEmpty(c.Name) ? "" : "#" + c.Name)}[{Short(c.Text)}]";

    private static double Luminance(Color c)
    {
        static double Lin(int v) { var s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);
    }

    private static double Contrast(Color a, Color b)
    {
        var l1 = Luminance(a); var l2 = Luminance(b);
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }

    private static Color Back(Control c)
    {
        for (var p = c; p is not null; p = p.Parent) if (p.BackColor.A == 255) return p.BackColor;
        return SystemColors.Control;
    }

    private static List<string> Audit(Control root)
    {
        var problems = new List<string>();
        foreach (var c in All(root).Where(Shown))
        {
            if (c.Parent is null) continue;
            if (c.Width <= 0 || c.Height <= 0) continue;   // collapsed on purpose (the hidden events of a carousel, an empty picture)

            // inside the parent (a scrolling parent may hold more than it shows)
            if (c.Dock == DockStyle.None && c.Parent is not TabControl && c.Parent is not ScrollableControl { AutoScroll: true })
            {
                var area = c.Parent.ClientRectangle;
                var r = c.Bounds;
                if (r.Right > area.Right + 1 || r.Bottom > area.Bottom + 1 || r.Left < -1 || r.Top < -1)
                    problems.Add($"outside parent: {Name(c)} {r} in {Name(c.Parent)} {area.Size}");
            }

            // text that does not fit
            if (c is Label or ButtonBase && !string.IsNullOrWhiteSpace(c.Text) && !(c is Label { AutoSize: true }))
            {
                var oneLine = c is Label { AutoEllipsis: true };   // cut with an ellipsis on purpose, but the line itself must fit
                var size = TextRenderer.MeasureText(c.Text, c.Font, new Size(oneLine ? int.MaxValue : Math.Max(1, c.ClientSize.Width), int.MaxValue), oneLine ? TextFormatFlags.NoPadding | TextFormatFlags.SingleLine : TextFormatFlags.NoPadding | TextFormatFlags.WordBreak);
                if (size.Height > c.ClientSize.Height + 3)
                    problems.Add($"text cut off: {Name(c)} needs {size.Height}px has {c.ClientSize.Height}px");
            }

            // readable colours
            if (c is Label or ButtonBase or TextBoxBase && !string.IsNullOrWhiteSpace(c.Text) && c.ForeColor.A == 255)
            {
                var ratio = Contrast(c.ForeColor, Back(c));
                if (ratio < 3.0) problems.Add($"low contrast {ratio:0.0}: {Name(c)}");
            }
        }

        // siblings on top of each other
        foreach (var parent in new[] { root }.Concat(All(root)).Where(p => p.Controls.Count > 1 && Shown(p) && p is not TabControl))
        {
            var kids = parent.Controls.Cast<Control>().Where(k => k.Visible && k.Width > 0 && k.Height > 0 && k is not Splitter).ToList();
            for (var i = 0; i < kids.Count; i++)
                for (var j = i + 1; j < kids.Count; j++)
                {
                    if (kids[i].Dock == DockStyle.Fill || kids[j].Dock == DockStyle.Fill) continue;
                    var o = Rectangle.Intersect(kids[i].Bounds, kids[j].Bounds);
                    if (o.Width > 3 && o.Height > 3)
                        problems.Add($"overlap: {Name(kids[i])} and {Name(kids[j])} ({o.Width}x{o.Height}) in {Name(parent)}");
                }
        }
        return problems;
    }

    private static int DistinctColours(Control c)
    {
        using var bmp = new Bitmap(c.Width, c.Height);
        c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height));
        var seen = new HashSet<int>();
        for (var x = 0; x < bmp.Width; x += 7)
            for (var y = 0; y < bmp.Height; y += 7) seen.Add(bmp.GetPixel(x, y).ToArgb());
        return seen.Count;
    }

    private static readonly DateOnly[] Days =
    {
        new(2026, 12, 6), new(2026, 8, 15), new(2026, 3, 25), new(2026, 7, 20), new(2026, 2, 28), new(2026, 11, 8), new(2026, 5, 21),
    };

    [Theory]
    [MemberData(nameof(Sizes))]
    public void NoLayoutHasVisibleDefectsInAnyThemeOrSize(int width, int height)
    {
        var report = new List<string>();
        foreach (var theme in new[] { "dark", "light" })
        {
            ChronikologioTheme.Set(theme);
            foreach (var region in new[] { "gr", "cy" })
                foreach (var layout in UiSettings.LayoutNames)
                    foreach (var day in Days)
                    {
                        using var window = Open(width, height);
                        window.PerformLayout();
                        window.ChooseTheme(theme);
                        window.ChooseRegion(region);
                        window.ChooseLayout(layout);
                        window.SetDate(day);
                        Application.DoEvents();
                        window.PerformLayout();
                        var where = $"{width}x{height} {theme} {region} {layout}";
                        foreach (var p in Audit(window).Distinct()) report.Add($"{where} {day:MM-dd}: {p}");
                        if (DistinctColours(window) < 6) report.Add($"{where} {day:MM-dd}: window looks blank");
                    }
        }
        // the same defect on several days is listed once
        var grouped = report.Select(r => r[..r.IndexOf(' ', r.IndexOf(' ', r.IndexOf(' ') + 1) + 1)] + r[(r.IndexOf(": ", StringComparison.Ordinal))..]).Distinct().ToList();
        Assert.True(grouped.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, grouped.Take(60)) + $"{Environment.NewLine}({grouped.Count} different problems, {report.Count} in total)");
    }

    [Fact]
    public void TheWindowCannotBeMadeSmallerThanWhatFitsItsControls()
    {
        using var window = Open(1100, 700);
        Assert.True(window.MinimumSize.Width >= 640 && window.MinimumSize.Height >= 480, window.MinimumSize.ToString());
        window.ClientSize = window.MinimumSize;
        Application.DoEvents();
        window.PerformLayout();
        Assert.Empty(Audit(window).Where(p => p.StartsWith("outside parent") || p.StartsWith("text cut off")));
    }

    [Fact]
    public void DarkThemeUsesDarkBackgroundsInEveryLayout()
    {
        ChronikologioTheme.Set("dark");
        foreach (var layout in UiSettings.LayoutNames)
        {
            using var window = Open(1100, 700);
            window.PerformLayout();
            window.ChooseLayout(layout);
            foreach (var c in All(window).Where(Shown).OfType<ScrollableControl>().Where(s => s.BackColor.A == 255))
                Assert.True(Luminance(c.BackColor) < 0.25, $"{layout}: {Name(c)} has a light background {c.BackColor} in the dark theme");
            foreach (var tab in All(window).OfType<TabControl>())
                Assert.True(Luminance(tab.BackColor) < 0.25, $"{layout}: tab control {Name(tab)} is light in the dark theme");
        }
    }

    /// <summary>Draws the whole window per layout and theme into PNG files (only when CHRON_RENDER_DIR is set) and reports how many controls the audit looked at.</summary>
    [Fact]
    public void RenderWindows()
    {
        var dir = Environment.GetEnvironmentVariable("CHRON_RENDER_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        foreach (var theme in new[] { "dark", "light" })
        {
            ChronikologioTheme.Set(theme);
            foreach (var layout in UiSettings.LayoutNames)
            {
                using var window = Open(1100, 700);
                window.ChooseTheme(theme);
                window.PerformLayout();
                window.ChooseLayout(layout);
                window.SetDate(new DateOnly(2026, 12, 6));
                Application.DoEvents();
                window.PerformLayout();
                using var bmp = new Bitmap(window.Width, window.Height);
                window.DrawToBitmap(bmp, new Rectangle(Point.Empty, window.Size));
                bmp.Save(Path.Combine(dir, $"window-{theme}-{layout}.png"), System.Drawing.Imaging.ImageFormat.Png);
                File.AppendAllText(Path.Combine(dir, "audit.txt"), $"{theme} {layout}: {All(window).Count()} controls, {All(window).Count(Shown)} shown{Environment.NewLine}");
            }
        }
    }
}
