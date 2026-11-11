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
        Assert.True(events.Count >= 2);
        Assert.True(events[0].IsGreek);
        Assert.Contains(events, e => e.Year == 1974 && e.IsGreek);
        Assert.DoesNotContain(events.SkipWhile(e => e.IsGreek), e => e.IsGreek);   // once a world event comes, no Greek one follows
    }

    [Fact]
    public void FeedListsGreekHistoryFirst()
    {
        var items = SanSimeraFeed.Build(Store, new DateOnly(2026, 7, 20)).Where(i => i.Kind == FeedKind.Event).ToList();
        Assert.Contains(items, i => i.Text.Contains("1974"));
        Assert.Contains(items, i => i.Text.Contains("1969"));
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
