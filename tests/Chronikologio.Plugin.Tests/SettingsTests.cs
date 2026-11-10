using Chronikologio.Core;
using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public class SettingsTests : IDisposable
{
    private readonly string _settings = Path.Combine(Path.GetTempPath(), "chronikologio-set-" + Guid.NewGuid().ToString("N") + ".json");
    private static string DataRoot() => Path.Combine(AppContext.BaseDirectory, "data");

    public void Dispose()
    {
        Loc.Set("el");
        ChronikologioTheme.Set("system");
        if (File.Exists(_settings)) File.Delete(_settings);
    }

    private MainWindow Open()
    {
        var w = new MainWindow(DataRoot(), _settings) { StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000), ShowInTaskbar = false, Opacity = 0 };
        w.Show();
        Application.DoEvents();
        return w;
    }

    [Fact]
    public void TheRailHasASettingsButtonBeforeAbout()
    {
        using var w = Open();
        var rail = w.Controls.OfType<SideRail>().Single();
        var keys = rail.Items.Select(i => i.Key).ToList();
        Assert.Equal("help", keys[^1]);
        Assert.Equal("settings", keys[^2]);
        Assert.Contains("(Ctrl+,)", rail.Items[^2].Tip);
    }

    [Fact]
    public void RenderSettingsWindow()
    {
        var dir = Environment.GetEnvironmentVariable("CHRON_RENDER_DIR");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        foreach (var theme in new[] { "dark", "light" })
        {
            ChronikologioTheme.Set(theme);
            using var w = Open();
            using var dialog = new SettingsDialog(w) { StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000), Opacity = 0 };
            dialog.Show();
            Application.DoEvents();
            using var bmp = new Bitmap(dialog.ClientSize.Width, dialog.ClientSize.Height);
            dialog.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
            bmp.Save(Path.Combine(dir, "settings-" + theme + ".png"));
        }
    }

    [Fact]
    public void TheSettingsWindowChangesTheThemeLayoutAndLanguageAtOnceAndResets()
    {
        using var w = Open();
        using var dialog = new SettingsDialog(w);
        dialog.Show();
        Application.DoEvents();
        Assert.Contains("dark", dialog.Themes);

        dialog.Pick("theme", "light");
        Assert.Equal("light", ChronikologioTheme.Name);
        dialog.Pick("layout", "tabs");
        Assert.Equal("tabs", w.Layout);
        dialog.Pick("language", "en");
        Assert.Equal("en", Loc.Language);
        dialog.Pick("region", "cy");
        Application.DoEvents();
        Assert.Equal("cy", w.Region);
        Assert.Equal("en", Loc.Language);   // the language the user chose stays when the country changes

        dialog.Reset();
        Application.DoEvents();
        Assert.Equal("system", ChronikologioTheme.Name);
        Assert.Equal("day", w.Layout);
        Assert.Equal("auto", w.LanguageSetting);
        Assert.Equal("cy", w.Region);   // the reset is about the look: the country stays
    }
}
