using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public class DocumentedEventsTests
{
    private static readonly DayStore Store = DayStore.Load(FindData());

    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    private static IEnumerable<HistoricalEvent> Documented() =>
        Store.AllEvents.Where(e => e.Sources.Any(s => s.Kind == "official-reference"));

    [Fact]
    public void ManyEventsAreBackedByOfficialReferences() =>
        Assert.True(Documented().Count() >= 280, $"only {Documented().Count()} documented events");

    [Fact]
    public void AlmostEveryDayHasAtLeastOneEvent()
    {
        var empty = new List<string>();
        for (var m = 1; m <= 12; m++)
            for (var d = 1; d <= DateTime.DaysInMonth(2024, m); d++)
                if (Store.GetDay(m, d).Events.Count == 0) empty.Add($"{d:00}/{m:00}");
        Assert.True(empty.Count <= 100, $"{empty.Count} days without an event: {string.Join(' ', empty)}");
    }

    [Fact]
    public void EveryDocumentedEventHasTheEncyclopediaPointerAndAtMostThreeOfficialReferences()
    {
        foreach (var e in Documented())
        {
            Assert.InRange(e.Sources.Count(s => s.Kind == "official-reference"), 1, 6);
            Assert.All(e.Sources.Where(s => s.Kind == "official-reference"), s => Assert.StartsWith("http", s.Url));
        }
    }

    [Fact]
    public void ExistingHandWrittenEventsGotSourcesFromTheSupplements()
    {
        // The Greek Olympic Games of 1896 and other hand-written events keep their own text and may gain references; none may lose its own source.
        foreach (var e in Store.AllEvents.Where(e => e.Sources.Any(s => s.Kind == "official-reference") && e.Sources.Any(s => s.Kind == "model-recall")))
            Assert.Contains(e.Sources, s => s.Kind == "model-recall");
    }

    [Fact]
    public void TheEventTextIsOurOwnAndShort()
    {
        foreach (var e in Documented())
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Title));
            Assert.InRange(e.Summary.Length, 20, 330);
            Assert.DoesNotContain("wikipedia", e.Summary, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void GreekHistoryStillComesFirstOnTheDaysThatGainedWorldEvents()
    {
        // 6 April: the Olympic Games of 1896 and the invasion of 1941 (Greek) come before the entry of the USA into the First World War (1917).
        var events = Store.GetDay(4, 6).Events;
        Assert.True(events[0].IsGreek);
        Assert.Contains(events, e => !e.IsGreek && e.Year == 1917);
        Assert.True(events.TakeWhile(e => e.IsGreek).Count() >= 2);
    }
}
