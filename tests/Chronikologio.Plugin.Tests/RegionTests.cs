using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public class RegionTests : IDisposable
{
    private readonly string _settings = Path.Combine(Path.GetTempPath(), "chronikologio-region-" + Guid.NewGuid().ToString("N") + ".json");
    private static string DataRoot() => Path.Combine(AppContext.BaseDirectory, "data");

    public void Dispose()
    {
        if (File.Exists(_settings)) File.Delete(_settings);
        ChronikologioTheme.Set("dark");
    }

    [Fact]
    public void TheCalendarOfCyprusCanBeChosenAndIsRemembered()
    {
        using (var window = new MainWindow(DataRoot(), _settings))
        {
            Assert.Equal("gr", window.Region);
            window.ChooseRegion("cy");
            Assert.Equal("cy", window.Region);
            window.ChooseRegion("nonsense");              // refused
            Assert.Equal("cy", window.Region);
        }
        Assert.Equal("cy", UiSettings.Load(_settings).Region);

        using var again = new MainWindow(DataRoot(), _settings);
        Assert.Equal("cy", again.Region);                 // restored at start-up
    }

    [Fact]
    public void ThePluginOutputCarriesTheCypriotData()
    {
        Assert.True(Directory.Exists(Path.Combine(DataRoot(), "orthodox-cy")));
        Assert.True(File.Exists(Path.Combine(DataRoot(), "births", "births-cy.tsv")));
    }

    [Fact]
    public void BrokenRegionInTheSettingsFallsBackToGreece()
    {
        File.WriteAllText(_settings, "{ \"Theme\": \"dark\", \"Region\": \"mars\" }");
        Assert.Equal("gr", UiSettings.Load(_settings).Region);
    }
}
