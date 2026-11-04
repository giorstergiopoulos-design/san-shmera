using System.Drawing.Imaging;
using Chronikologio.Core;
using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

/// <summary>Draws the panels into PNG files so the look can be checked without opening the app. Runs only when CHRON_RENDER_DIR is set.</summary>
[Collection("theme")]
public class RenderPreviewTests
{
    private static string DataRoot() => Path.Combine(AppContext.BaseDirectory, "data");

    private static void Save(Control c, string file)
    {
        _ = c.Handle;
        c.PerformLayout();
        using var bmp = new Bitmap(c.Width, c.Height);
        c.DrawToBitmap(bmp, new Rectangle(0, 0, c.Width, c.Height));
        bmp.Save(file, ImageFormat.Png);
    }

    [Fact]
    public void RenderPanelsAndDetails()
    {
        var dir = Environment.GetEnvironmentVariable("CHRON_RENDER_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        var store = DayStore.Load(DataRoot());
        foreach (var theme in new[] { "dark", "light" })
        {
            ChronikologioTheme.Set(theme);
            foreach (var date in new[] { new DateOnly(2026, 12, 6), new DateOnly(2026, 7, 9), new DateOnly(2026, 3, 25) })
            {
                var panel = new CalendarPanel(store) { Size = new Size(380, 560) };
                panel.SelectedDate = date;
                Save(panel, Path.Combine(dir, $"panel-{theme}-{date:MMdd}.png"));

                var view = new DayDetailsView { Size = new Size(700, 640), BackColor = panel.BackColor, ForeColor = panel.ForeColor };
                view.ShowDay(store, date, full: true);
                Save(view, Path.Combine(dir, $"details-{theme}-{date:MMdd}.png"));
            }
        }
        foreach (var theme in new[] { "dark", "light" })
        {
            ChronikologioTheme.Set(theme);
            foreach (var name in UiSettings.LayoutNames)
            {
                var calendar = new CalendarPanel(store);
                DayLayout layout = name switch
                {
                    "feed" => new FeedLayout(store),
                    "tabs" => new TabsLayout(store, calendar),
                    _ => new CardsLayout(store)
                };
                layout.Size = new Size(1000, 640);
                layout.SelectedDate = new DateOnly(2026, 12, 6);
                Save(layout, Path.Combine(dir, $"layout-{name}-{theme}.png"));
                layout.Dispose();
            }
        }
        var settings = Path.Combine(dir, "render-settings.json");
        if (File.Exists(settings)) File.Delete(settings);
        foreach (var theme in new[] { "dark", "light" })
        {
            ChronikologioTheme.Set(theme);
            foreach (var name in UiSettings.LayoutNames)
            {
                using var window = new MainWindow(DataRoot(), settings);
                window.ClientSize = new Size(1120, 740);
                window.ChooseLayout(name);
                window.SetDate(new DateOnly(2026, 12, 6));
                window.ShowInTaskbar = false;
                window.StartPosition = FormStartPosition.Manual;
                window.Location = new Point(-3000, 0);
                window.Show();
                Application.DoEvents();
                Save(window, Path.Combine(dir, $"window-{name}-{theme}.png"));
                window.Hide();
            }
        }
        if (File.Exists(settings)) File.Delete(settings);
        ChronikologioTheme.Set("dark");
    }
}
