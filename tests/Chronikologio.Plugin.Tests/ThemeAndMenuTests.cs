using Chronikologio.UI;
using Chronikologio.Core;
using Opsis.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public class ThemeAndMenuTests : IDisposable
{
    private readonly string _settings = Path.Combine(Path.GetTempPath(), "chronikologio-test-" + Guid.NewGuid().ToString("N") + ".json");
    private static string DataRoot() => Path.Combine(AppContext.BaseDirectory, "data");

    public void Dispose()
    {
        if (File.Exists(_settings)) File.Delete(_settings);
        ChronikologioTheme.Set("dark");
    }

    [Fact]
    public void LightAndDarkAreTheSharedOpsisPalettes()
    {
        Assert.True(ChronikologioTheme.Set("dark"));
        Assert.True(ChronikologioTheme.IsDark);
        Assert.Equal(OpsisPalette.Dark.Background, OpsisTheme.Current.Background);   // the Opsis.UI controls read the same tokens

        Assert.True(ChronikologioTheme.Set("light"));
        Assert.False(ChronikologioTheme.IsDark);
        Assert.Equal(OpsisPalette.Light.Surface, OpsisTheme.Current.Surface);
    }

    [Fact]
    public void NamedPalettesComeFromOpsisThemes()
    {
        ChronikologioTheme.Set("forest");
        Assert.Equal(OpsisThemes.Forest.Accent, OpsisTheme.Current.Accent);
        ChronikologioTheme.Set("dawn");
        Assert.False(ChronikologioTheme.IsDark);
    }

    [Fact]
    public void AnUnknownThemeNameChangesNothing()
    {
        ChronikologioTheme.Set("light");
        Assert.False(ChronikologioTheme.Set("neon"));
        Assert.Equal("light", ChronikologioTheme.Name);
    }

    [Fact]
    public void NextCyclesThroughEveryTheme()
    {
        ChronikologioTheme.Set(ChronikologioTheme.Names[0]);
        var seen = new HashSet<string> { ChronikologioTheme.Name };
        for (var i = 0; i < ChronikologioTheme.Names.Count - 1; i++) seen.Add(ChronikologioTheme.Next());
        Assert.Equal(ChronikologioTheme.Names.Count, seen.Count);
        Assert.Equal(ChronikologioTheme.Names[0], ChronikologioTheme.Next());   // and back to the start
    }

    [Fact]
    public void ThePanelRestylesWhenTheThemeChanges()
    {
        var store = DayStore.Load(DataRoot());
        using var panel = new CalendarPanel(store);
        ChronikologioTheme.Set("dark");
        var dark = panel.BackColor;
        ChronikologioTheme.Set("light");
        Assert.NotEqual(dark, panel.BackColor);
        Assert.Equal(OpsisPalette.Light.Background, panel.BackColor);
    }

    [Fact]
    public void HostColoursFromOpsisAreFollowed()
    {
        var store = DayStore.Load(DataRoot());
        using var panel = new CalendarPanel(store);
        ChronikologioTheme.SetFromHost(true, Color.FromArgb(10, 20, 30).ToArgb(), Color.FromArgb(40, 50, 60).ToArgb(), Color.Gray.ToArgb(),
            Color.White.ToArgb(), Color.Silver.ToArgb(), Color.Orange.ToArgb());
        Assert.Equal(Color.FromArgb(10, 20, 30), panel.BackColor);
        Assert.Equal("host", ChronikologioTheme.Name);
    }

    [Fact]
    public void TheMenuIsHiddenUntilCtrlM()
    {
        using var window = new MainWindow(DataRoot(), _settings);
        Assert.False(window.MenuVisible);
        Assert.True(window.HandleShortcut(Keys.Control | Keys.M));
        Assert.True(window.MenuVisible);
        Assert.True(window.HandleShortcut(Keys.Control | Keys.M));
        Assert.False(window.MenuVisible);
        Assert.False(window.HandleShortcut(Keys.A));
    }

    [Fact]
    public void CtrlTChangesTheThemeAndItIsRemembered()
    {
        using (var window = new MainWindow(DataRoot(), _settings))
        {
            window.ChooseTheme("light");
            window.HandleShortcut(Keys.Control | Keys.T);
            Assert.Equal("midnight", ChronikologioTheme.Name);   // the theme after "light" in the list
            window.HandleShortcut(Keys.Control | Keys.M);
        }
        var saved = UiSettings.Load(_settings);
        Assert.Equal("midnight", saved.Theme);
        Assert.True(saved.MenuShown);

        ChronikologioTheme.Set("dark");
        using var again = new MainWindow(DataRoot(), _settings);
        Assert.Equal("midnight", ChronikologioTheme.Name);   // restored at start-up
        Assert.True(again.MenuVisible);
    }

    [Fact]
    public void BrokenSettingsFallBackToDefaults()
    {
        File.WriteAllText(_settings, "{ not json");
        var s = UiSettings.Load(_settings);
        Assert.Equal("system", s.Theme);
        Assert.False(s.MenuShown);
    }
}

[CollectionDefinition("theme", DisableParallelization = true)]
public class ThemeCollection { }
