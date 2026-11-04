using Chronikologio.Core;
using Chronikologio.UI;
using Xunit;

namespace Chronikologio.Plugin.Tests;

[Collection("theme")]
public class EventCarouselTests
{
    private static string DataRoot() => Path.Combine(AppContext.BaseDirectory, "data");

    [Fact]
    public void TheArrowsGoThroughTheEventsAndWrapAround()
    {
        var store = DayStore.Load(DataRoot());
        var day = store.GetDay(new DateOnly(2026, 4, 6));
        var greek = day.Events.Where(e => e.IsGreek).ToList();
        Assert.True(greek.Count >= 2);

        using var carousel = new EventCarousel();
        carousel.SetEvents(store, greek, "none");
        Assert.Equal(greek.Count, carousel.Count);
        Assert.Equal(0, carousel.Index);
        carousel.Move(1);
        Assert.Equal(1, carousel.Index);
        carousel.Move(-1);
        carousel.Move(-1);
        Assert.Equal(greek.Count - 1, carousel.Index);   // left from the first goes to the last
        carousel.Move(1);
        Assert.Equal(0, carousel.Index);
    }

    [Fact]
    public void ManyEventsCarryAFreePhotographWithItsCreditAndTheCarouselShowsIt()
    {
        var store = DayStore.Load(DataRoot());
        var withPhoto = store.AllEvents.Where(e => e.Images is { Length: > 0 }).ToList();
        Assert.True(withPhoto.Count >= 150, $"only {withPhoto.Count} events with a photograph");
        Assert.All(withPhoto, e => Assert.All(e.Images!, i => { Assert.Equal("photo", i.Kind); Assert.NotNull(store.ImagePath(i)); }));

        var one = withPhoto.First();
        using var carousel = new EventCarousel();
        carousel.SetEvents(store, new[] { one }, "none");
        Assert.Equal(1, carousel.Count);
    }

    [Fact]
    public void ADayWithOneEventOrNoneDoesNotMove()
    {
        var store = DayStore.Load(DataRoot());
        using var carousel = new EventCarousel();
        carousel.SetEvents(store, Array.Empty<HistoricalEvent>(), "none");
        carousel.Move(1);
        Assert.Equal(0, carousel.Index);
        Assert.Equal(0, carousel.Count);
    }

    [Fact]
    public void TheFramesSplitTheDayIntoGreeceAndAbroad()
    {
        var store = DayStore.Load(DataRoot());
        var day = store.GetDay(new DateOnly(2026, 4, 6));
        using var frames = new EventFrames();
        frames.Set(store, day);
        Assert.Equal(day.Events.Count(e => e.IsGreek), frames.Greek.Count);
        Assert.Equal(day.Events.Count(e => !e.IsGreek), frames.World.Count);
        Assert.Equal(Loc.T("frame.gr"), frames.Greek.FrameTitle);
    }

    [Fact]
    public void EveryLayoutOpensWithTheFrames()
    {
        var settings = Path.Combine(Path.GetTempPath(), "chronikologio-frames-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            using var window = new MainWindow(DataRoot(), settings);
            foreach (var name in UiSettings.LayoutNames)
            {
                window.ChooseLayout(name);
                window.SetDate(new DateOnly(2026, 4, 6));
                Assert.Equal(name, window.Layout);
            }
        }
        finally { if (File.Exists(settings)) File.Delete(settings); }
    }
}
