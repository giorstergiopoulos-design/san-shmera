using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public class LivesTests
{
    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    [Fact]
    public void EveryLifeBelongsToExactlyOneCommemorationOfItsDay()
    {
        var data = FindData();
        var gr = DayStore.Load(data, "gr");
        var cy = DayStore.Load(data, "cy");
        Assert.Empty(gr.Errors);
        Assert.Empty(cy.Errors);
        var files = Directory.GetFiles(Path.Combine(data, "lives"), "lives*.tsv");
        Assert.NotEmpty(files);
        // a file whose name ends with -cy belongs to the Cyprus calendar only
        foreach (var (line, store) in files.SelectMany(file => File.ReadAllLines(file).Where(l => l.Length > 0 && !l.StartsWith('#')).Select(l => (l, Path.GetFileNameWithoutExtension(file).EndsWith("-cy") ? cy : gr))))
        {
            var f = line.Split('|');
            
            var matches = store.GetDay(int.Parse(f[0][..2]), int.Parse(f[0][3..])).Commemorations.Where(c => c.Title.StartsWith(f[1], StringComparison.Ordinal)).ToList();
            Assert.True(matches.Count == 1, $"{f[0]} {f[1]}: {matches.Count} matches");
            Assert.False(string.IsNullOrWhiteSpace(matches[0].Details));
        }
    }

    [Fact]
    public void ALifeIsLongerThanTheSummary()
    {
        var store = DayStore.Load(FindData());
        var c = store.GetDay(new DateOnly(2024, 12, 6)).Commemorations.First(x => x.Title.StartsWith("Άγιος Νικόλαος", StringComparison.Ordinal));
        Assert.True(c.Details!.Length > c.Summary.Length);
    }

    [Fact]
    public void EveryCommemorationOfTheCyprusCalendarHasALife()
    {
        var store = DayStore.Load(FindData(), "cy");
        var without = store.AllCommemorations.Where(c => string.IsNullOrWhiteSpace(c.Details)).Select(c => c.Title).ToList();
        Assert.True(without.Count == 0, "without a life: " + string.Join("; ", without.Take(20)) + $" ({without.Count})");
    }

    [Fact]
    public void DumpEventCounts()
    {
        var file = Environment.GetEnvironmentVariable("CHRON_DUMP");
        if (string.IsNullOrEmpty(file)) return;
        var store = DayStore.Load(FindData(), "gr");
        var lines = new List<string>();
        for (var i = 0; i < 366; i++)
        {
            var d = new DateOnly(2024, 1, 1).AddDays(i);
            var v = store.GetDay(d.Month, d.Day);
            lines.Add($"{d:MM-dd} gr={v.Events.Count(e => e.IsGreek)} world={v.Events.Count(e => !e.IsGreek)}");
        }
        File.WriteAllLines(file, lines);
    }

    [Fact]
    public void NoDayShowsTheSameEventTwice()
    {
        foreach (var region in new[] { "gr", "cy" })
        {
            var store = DayStore.Load(FindData(), region);
            for (var i = 0; i < 366; i++)
            {
                var d = new DateOnly(2024, 1, 1).AddDays(i);
                var day = store.GetDay(d.Month, d.Day);
                foreach (var group in day.Events.GroupBy(e => e.Year))
                {
                    var titles = group.Select(e => e.Title).ToList();
                    for (var a = 0; a < titles.Count; a++)
                        for (var b = a + 1; b < titles.Count; b++)
                        {
                            var wa = TextNorm.Fold(titles[a]).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 5).ToHashSet();
                            var wb = TextNorm.Fold(titles[b]).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 5).ToHashSet();
                            Assert.True(wa.Intersect(wb).Count() < 2, $"{region} {d:MM-dd} {group.Key}: {titles[a]} / {titles[b]}");
                        }
                }
            }
        }
    }

    [Fact]
    public void DumpDuplicateSuspects()
    {
        var file = Environment.GetEnvironmentVariable("CHRON_DUP");
        if (string.IsNullOrEmpty(file)) return;
        var lines = new List<string>();
        foreach (var region in new[] { "gr", "cy" })
        {
            var store = DayStore.Load(FindData(), region);
            for (var i = 0; i < 366; i++)
            {
                var d = new DateOnly(2024, 1, 1).AddDays(i);
                var ev = store.GetDay(d.Month, d.Day).Events.ToList();
                for (var a = 0; a < ev.Count; a++)
                    for (var b = a + 1; b < ev.Count; b++)
                    {
                        static HashSet<string> W(string s) => TextNorm.Fold(s).Split(new[] { ' ', '«', '»', '.', ',', '"' }, StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 6).ToHashSet();
                        var shared = W(ev[a].Title).Intersect(W(ev[b].Title)).ToList();
                        if (shared.Count >= 1 && Math.Abs((ev[a].Year ?? 0) - (ev[b].Year ?? 0)) <= 3)
                            lines.Add($"{region} {d:MM-dd} {ev[a].Year} [{ev[a].Title}] ~ {ev[b].Year} [{ev[b].Title}]");
                    }
            }
        }
        File.WriteAllLines(file, lines);
    }
}
