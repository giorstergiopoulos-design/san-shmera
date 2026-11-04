using Chronikologio.Core;
using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public class FlagAndLanguageTests : IDisposable
{
    private readonly string _settings = Path.Combine(Path.GetTempPath(), "chronikologio-flag-" + Guid.NewGuid().ToString("N") + ".json");
    private static string DataRoot() => Path.Combine(AppContext.BaseDirectory, "data");

    public void Dispose()
    {
        if (File.Exists(_settings)) File.Delete(_settings);
        Loc.Set("el");
    }

    [Fact]
    public void EveryCountryHasAFlagAndALanguageTable()
    {
        foreach (var country in Countries.All)
        {
            using var flag = Flags.Create(country.Region);
            Assert.Equal(30, flag.Width);
            Assert.True(Loc.Set(country.Language), country.Region);
            Assert.NotEqual("region." + country.Region, Loc.T("region." + country.Region));
        }
    }

    [Fact]
    public void TheLanguageOfACountryIsItsFirstOfficialLanguage()
    {
        Assert.Equal("el", Countries.LanguageOf("gr"));
        Assert.Equal("el", Countries.LanguageOf("cy"));   // Greek first, Turkish second
        Assert.Equal("el", Countries.LanguageOf("zz"));
    }

    [Fact]
    public void AnUnknownLanguageChangesNothingAndMissingKeysFallBackToGreek()
    {
        Assert.False(Loc.Set("xx"));
        Assert.Equal("el", Loc.Language);
        Assert.Equal("no.such.key", Loc.T("no.such.key"));
    }

    [Fact]
    public void ChoosingACountryWithThePanelSwitchesTheDatabaseAndSavesIt()
    {
        var panel = new CalendarPanel(DayStore.Load(DataRoot(), "gr")) { SettingsPath = _settings };
        DayStore? seen = null;
        panel.StoreChanged += s => seen = s;

        panel.ChooseRegion("cy");

        Assert.Equal("cy", panel.Region);
        Assert.Equal("cy", seen?.Region);
        Assert.Equal("cy", UiSettings.Load(_settings).Region);
        Assert.Equal("el", Loc.Language);
    }

    [Fact]
    public void ChoosingTheSameOrAnUnknownCountryDoesNothing()
    {
        var panel = new CalendarPanel(DayStore.Load(DataRoot(), "gr")) { SettingsPath = _settings };
        var raised = 0;
        panel.StoreChanged += _ => raised++;
        panel.ChooseRegion("gr");
        panel.ChooseRegion("zz");
        Assert.Equal(0, raised);
        Assert.False(File.Exists(_settings));
    }

    [Fact]
    public void TheWindowFollowsTheFlagButton()
    {
        using var window = new MainWindow(DataRoot(), _settings);
        window.ChooseRegion("cy");
        Assert.Equal("cy", window.Region);
        Assert.Equal("cy", UiSettings.Load(_settings).Region);
    }
}
