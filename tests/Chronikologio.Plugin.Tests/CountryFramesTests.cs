using Chronikologio.Core;
using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public class CountryFramesTests : IDisposable
{
    private readonly string _settings = Path.Combine(Path.GetTempPath(), "chronikologio-frames-" + Guid.NewGuid().ToString("N") + ".json");
    private static string DataRoot() => Path.Combine(AppContext.BaseDirectory, "data");
    public void Dispose() { if (File.Exists(_settings)) File.Delete(_settings); Loc.Set("el"); }

    private static IEnumerable<Control> All(Control root)
    {
        foreach (Control c in root.Controls) { yield return c; foreach (var d in All(c)) yield return d; }
    }

    [Theory]
    [InlineData("cards")]
    [InlineData("feed")]
    [InlineData("tabs")]
    public void TheFirstFrameFollowsTheFlag(string layout)
    {
        using var window = new MainWindow(DataRoot(), _settings) { StartPosition = FormStartPosition.Manual, Location = new Point(-32000, -32000), ShowInTaskbar = false, Opacity = 0 };
        window.Show();
        window.ChooseLayout(layout);
        window.SetDate(new DateOnly(2026, 8, 14));

        window.ChooseRegion("gr");
        Application.DoEvents();
        var titles = All(window).OfType<EventCarousel>().Select(c => c.FrameTitle).ToList();
        Assert.Contains("Σαν σήμερα στην Ελλάδα", titles);
        Assert.Contains("Σαν σήμερα στο εξωτερικό", titles);

        window.ChooseRegion("cy");
        Application.DoEvents();
        titles = All(window).OfType<EventCarousel>().Select(c => c.FrameTitle).ToList();
        Assert.Contains("Σαν σήμερα στην Κύπρο", titles);
        Assert.DoesNotContain("Σαν σήμερα στην Ελλάδα", titles);
        Assert.Contains("Σαν σήμερα στο εξωτερικό", titles);
    }

    [Fact]
    public void InTheCyprusCalendarTheDomesticFrameHoldsOnlyCypriotEventsAndGreeceMovesAbroad()
    {
        var cy = DayStore.Load(DataRoot(), "cy");
        var day = cy.GetDay(8, 14);   // Solomos Solomou (1996), the second phase of the invasion (1974), and Greek / world events
        var domestic = day.Events.Where(e => Countries.IsDomestic("cy", e)).ToList();
        var abroad = day.Events.Where(e => !Countries.IsDomestic("cy", e)).ToList();
        Assert.NotEmpty(domestic);
        Assert.All(domestic, e => Assert.Equal("Κύπρος", e.Region));
        Assert.DoesNotContain(abroad, e => e.Region == "Κύπρος");

        var gr = DayStore.Load(DataRoot(), "gr");
        Assert.All(gr.GetDay(5, 27).Events.Where(e => Countries.IsDomestic("gr", e)), e => Assert.True(e.IsGreek));
    }

    [Fact]
    public void TheShortTextNamesTheHistoryOfTheChosenCountry()
    {
        var cy = DayStore.Load(DataRoot(), "cy");
        Assert.Contains("Σαν σήμερα στην κυπριακή ιστορία", ShortText.Build(cy.GetDay(8, 14), "cy"));
        var gr = DayStore.Load(DataRoot(), "gr");
        Assert.Contains("Σαν σήμερα στην ελληνική ιστορία", ShortText.Build(gr.GetDay(5, 27), "gr"));
    }
}
