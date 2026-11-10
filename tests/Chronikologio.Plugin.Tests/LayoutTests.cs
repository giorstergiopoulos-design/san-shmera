using Chronikologio.Core;
using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public class LayoutTests : IDisposable
{
    private readonly string _settings = Path.Combine(Path.GetTempPath(), "chronikologio-layout-" + Guid.NewGuid().ToString("N") + ".json");
    private static string DataRoot() => Path.Combine(AppContext.BaseDirectory, "data");

    public void Dispose()
    {
        if (File.Exists(_settings)) File.Delete(_settings);
        Loc.Set("el");
        ChronikologioTheme.Set("dark");
    }

    [Fact]
    public void TheWindowStartsWithTheDayLayoutAndRemembersTheChoice()
    {
        using (var window = new MainWindow(DataRoot(), _settings))
        {
            Assert.Equal("day", window.Layout);
            window.ChooseLayout("feed");
            Assert.Equal("feed", window.Layout);
        }
        using var again = new MainWindow(DataRoot(), _settings);
        Assert.Equal("feed", again.Layout);
    }

    [Fact]
    public void EveryLayoutCanBeChosenAndTheCycleVisitsAllFour()
    {
        using var window = new MainWindow(DataRoot(), _settings);
        var seen = new HashSet<string> { window.Layout };
        for (var i = 0; i < 3; i++) { window.CycleLayout(); seen.Add(window.Layout); }
        Assert.Equal(new[] { "day", "cards", "feed", "tabs" }.OrderBy(x => x), seen.OrderBy(x => x));
        window.CycleLayout();
        Assert.Equal(UiSettings.LayoutNames[(UiSettings.LayoutNames.ToList().IndexOf("tabs") + 1) % UiSettings.LayoutNames.Count], window.Layout);
    }

    [Fact]
    public void AnUnknownLayoutChangesNothing()
    {
        using var window = new MainWindow(DataRoot(), _settings);
        window.ChooseLayout("moon");
        Assert.Equal("day", window.Layout);
    }

    [Fact]
    public void TheChosenDayIsKeptWhenTheLayoutChanges()
    {
        using var window = new MainWindow(DataRoot(), _settings);
        var day = new DateOnly(2026, 8, 15);
        window.SetDate(day);
        foreach (var name in UiSettings.LayoutNames)
        {
            window.ChooseLayout(name);
            Assert.Equal(day, window.SelectedDate);
        }
    }

    [Fact]
    public void ChoosingACountryInTheWindowReachesEveryLayout()
    {
        using var window = new MainWindow(DataRoot(), _settings);
        foreach (var name in UiSettings.LayoutNames)
        {
            window.ChooseLayout(name);
            window.ChooseRegion("cy");
            Assert.Equal("cy", window.Region);
            window.ChooseRegion("gr");
            Assert.Equal("gr", window.Region);
        }
    }

    [Fact]
    public void TheTabsLayoutReleasesTheSharedCalendarWhenItIsLeft()
    {
        using var window = new MainWindow(DataRoot(), _settings);
        window.ChooseLayout("tabs");
        window.ChooseLayout("cards");
        window.ChooseLayout("tabs");   // the calendar panel was not disposed with the first tabs layout
        Assert.Equal("tabs", window.Layout);
    }

    [Fact]
    public void TheSettingsFileFallsBackToTheDayLayoutForAnUnknownLayout()
    {
        File.WriteAllText(_settings, "{ \"Layout\": \"mars\" }");
        Assert.Equal("day", UiSettings.Load(_settings).Layout);
    }

    [Fact]
    public void TheNameListOfTheTabsLayoutKnowsEveryName()
    {
        var store = DayStore.Load(DataRoot());
        Assert.Contains(store.AllNames(), n => n.Name.Name == "Νεκτάριος" && n.Month == 11 && n.Day == 9);
        Assert.True(store.AllNames().Count() > 300);
    }

    [Fact]
    public void ShortTextHelpersFeedTheCards()
    {
        var day = DayStore.Load(DataRoot()).GetDay(11, 9);
        Assert.Contains("Νεκτάριος", ShortText.NamesLine(day));
        Assert.Contains("Νεκτάριος", ShortText.SaintText(day));
        Assert.NotEmpty(ShortText.EventsText(DayStore.Load(DataRoot()).GetDay(3, 25).Events.Where(e => e.IsGreek)));
    }
}
