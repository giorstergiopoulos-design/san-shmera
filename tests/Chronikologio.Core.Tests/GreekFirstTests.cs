using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public class GreekFirstTests
{
    private static readonly DayStore Store = DayStore.Load(FindData());

    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    [Fact]
    public void GreekEventsComeBeforeWorldEventsEvenWhenLater()
    {
        // 20 July: Apollo 11 (1969, world) and the invasion of Cyprus (1974, Greek).
        var events = Store.GetDay(7, 20).Events;
        Assert.Equal(2, events.Count);
        Assert.True(events[0].IsGreek);
        Assert.Equal(1974, events[0].Year);
        Assert.False(events[1].IsGreek);
    }

    [Fact]
    public void FeedListsGreekHistoryFirst()
    {
        var items = SanSimeraFeed.Build(Store, new DateOnly(2026, 7, 20)).Where(i => i.Kind == FeedKind.Event).ToList();
        Assert.Contains("1974", items[0].Text);
        Assert.Contains("1969", items[1].Text);
    }

    [Fact]
    public void ShortTextNamesBothGreekAndWorldHistory()
    {
        var text = ShortText.Build(Store.GetDay(7, 20));
        var greek = text.IndexOf("ελληνική ιστορία", StringComparison.Ordinal);
        var world = text.IndexOf("παγκόσμια ιστορία", StringComparison.Ordinal);
        Assert.True(greek >= 0 && world > greek);
    }

    [Fact]
    public void EveryMonthHasGreekHistory()
    {
        for (var m = 1; m <= 12; m++)
            Assert.Contains(Enumerable.Range(1, DateTime.DaysInMonth(2025, m)).SelectMany(d => Store.GetDay(m, d).Events), e => e.IsGreek);
    }
}
