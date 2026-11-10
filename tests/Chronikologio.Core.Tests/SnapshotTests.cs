using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public class SnapshotTests
{
    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    private static bool WaitFor(Func<bool> condition, int seconds = 20)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < end) { if (condition()) return true; Thread.Sleep(20); }
        return condition();
    }

    [Fact]
    public void StartReturnsAtOnceAndTheSnapshotArrivesInTheBackground()
    {
        var today = new DateOnly(2026, 7, 20);
        using var service = new DailySnapshotService(FindData(), "gr", () => today);
        var raised = 0;
        service.Changed += () => Interlocked.Increment(ref raised);
        Assert.Null(service.Current);
        service.Start();
        Assert.True(WaitFor(() => service.Current is not null), "no snapshot");
        Assert.Equal(today, service.Current!.Date);
        Assert.Equal("gr", service.Current.Region);
        Assert.NotEmpty(service.Current.Items);
        Assert.True(raised >= 1);
    }

    [Fact]
    public void ANewDayOrANewCountryMakesANewSnapshot()
    {
        var today = new DateOnly(2026, 7, 20);
        using var service = new DailySnapshotService(FindData(), "gr", () => today);
        var first = service.BuildNow();
        Assert.Equal(today, first.Date);

        today = new DateOnly(2026, 7, 21);   // the clock passed midnight
        service.CheckDate();
        Assert.True(WaitFor(() => service.Current!.Date == today), "no snapshot of the new day");

        service.SetRegion("cy");
        Assert.True(WaitFor(() => service.Current!.Region == "cy"), "no snapshot for Cyprus");
        Assert.Equal("cy", service.Store.Region);
    }

    [Fact]
    public void TheLinesForTheBarsAreShortAndFew()
    {
        var store = DayStore.Load(FindData(), "gr");
        for (var i = 0; i < 366; i++)
        {
            var date = new DateOnly(2024, 1, 1).AddDays(i);
            var items = SanSimeraFeed.BuildForTicker(store, date);
            Assert.True(items.Count(x => x.Kind == FeedKind.Saint) <= 3, $"{date:MM-dd}: saints");
            Assert.True(items.Count(x => x.Kind == FeedKind.Event) <= 4, $"{date:MM-dd}: events");
            Assert.All(items.Where(x => x.Kind != FeedKind.NameDays), x => Assert.True(x.Text.Length <= 201, $"{date:MM-dd}: {x.Text.Length} characters"));
        }
    }

    [Fact]
    public void TheNamesLineIsNeverCut()
    {
        var store = DayStore.Load(FindData(), "gr");
        var day = new DateOnly(2026, 1, 1);
        var all = SanSimeraFeed.Build(store, day).First(x => x.Kind == FeedKind.NameDays).Text;
        var ticker = SanSimeraFeed.BuildForTicker(store, day).First(x => x.Kind == FeedKind.NameDays).Text;
        Assert.Equal(all, ticker);
    }
}
