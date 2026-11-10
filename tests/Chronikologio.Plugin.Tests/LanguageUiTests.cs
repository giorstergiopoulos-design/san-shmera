using Chronikologio.Core;
using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public class LanguageUiTests : IDisposable
{
    private readonly string _settings = Path.Combine(Path.GetTempPath(), "chronikologio-lang-" + Guid.NewGuid().ToString("N") + ".json");
    private static string DataRoot() => Path.Combine(AppContext.BaseDirectory, "data");

    public void Dispose()
    {
        Loc.Set("el");
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
    public void TheLanguageIsChosenApartFromTheCountryAndRemembered()
    {
        using (var w = Open())
        {
            Assert.Equal("auto", w.LanguageSetting);
            Assert.Equal("el", Loc.Language);
            w.ChooseLanguage("en");
            Assert.Equal("en", Loc.Language);
            Assert.Contains("Chronikologio", w.Text);
            w.ChooseRegion("cy");
            Application.DoEvents();
            Assert.Equal("cy", w.Region);
            Assert.Equal("en", Loc.Language);   // the country changed, the chosen language did not
        }
        Loc.Set("el");
        using (var again = Open())
        {
            Assert.Equal("en", again.LanguageSetting);
            Assert.Equal("en", Loc.Language);
            again.ChooseLanguage("auto");
            Assert.Equal("el", Loc.Language);
        }
    }
}
