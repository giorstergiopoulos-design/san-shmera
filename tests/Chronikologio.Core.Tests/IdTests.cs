using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public class IdTests
{
    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    private static IEnumerable<(string Kind, int Month, int Day, int? Year, string Title, string? Id)> Entries(DayStore store)
    {
        for (var i = 0; i < 366; i++)
        {
            var d = new DateOnly(2024, 1, 1).AddDays(i);
            var day = store.GetDay(d.Month, d.Day);
            foreach (var c in day.Commemorations) yield return ("saint", d.Month, d.Day, null, c.Title, c.Id);
            foreach (var e in day.Events) yield return ("event", d.Month, d.Day, e.Year, e.Title, e.Id);
            foreach (var p in day.People) yield return ("birth", d.Month, d.Day, p.Year, p.Name, p.Id);
            foreach (var p in day.DeathList) yield return ("death", d.Month, d.Day, p.Year, p.Name, p.Id);
        }
    }

    [Fact]
    public void TheIdOfAnEntryComesFromItsContent()
    {
        var a = EntryIds.Make("event", 12, 6, 1917, "Η Φινλανδία κηρύσσει την ανεξαρτησία της");
        Assert.Equal(a, EntryIds.Make("event", 12, 6, 1917, "Η  ΦΙΝΛΑΝΔΙΑ κηρύσσει την ανεξαρτησία της"));   // case and accents do not matter
        Assert.NotEqual(a, EntryIds.Make("event", 12, 7, 1917, "Η Φινλανδία κηρύσσει την ανεξαρτησία της"));
        Assert.StartsWith("event:12-06:1917:", a);
        Assert.StartsWith("saint:12-06:x:", EntryIds.Make("saint", 12, 6, null, "Άγιος Νικόλαος"));
    }

    [Fact]
    public void EveryEntryHasAnIdThatIsUniqueAndInTheRegistry()
    {
        var data = FindData();
        var registry = EntryIds.LoadRegistry(data);
        Assert.True(registry.Count > 0, "data/ids/ids.tsv is missing: write it with CHRON_IDS=<path> dotnet test --filter DumpIdRegistry");
        foreach (var region in new[] { "gr", "cy" })
        {
            var all = Entries(DayStore.Load(data, region)).ToList();
            Assert.DoesNotContain(all, e => string.IsNullOrEmpty(e.Id));
            var duplicates = all.GroupBy(e => e.Id).Where(g => g.Count() > 1).Select(g => g.Key + " " + g.First().Title).Take(10).ToList();
            Assert.True(duplicates.Count == 0, region + ": duplicate ids: " + string.Join("; ", duplicates));
            var missing = all.Where(e => !registry.ContainsKey(EntryIds.Key(e.Kind, e.Month, e.Day, e.Year, e.Title))).Select(e => $"{e.Kind} {e.Month:00}-{e.Day:00} {e.Year} {e.Title}").Take(10).ToList();
            Assert.True(missing.Count == 0, region + ": not in the registry (regenerate it with CHRON_IDS): " + string.Join("; ", missing));
        }
    }

    [Fact]
    public void DumpIdRegistry()
    {
        var file = Environment.GetEnvironmentVariable("CHRON_IDS");
        if (string.IsNullOrEmpty(file)) return;
        var data = FindData();
        // keep the identifiers already given, add the new ones
        var old = EntryIds.LoadRegistry(data);
        var rows = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var region in new[] { "gr", "cy" })
            foreach (var e in Entries(DayStore.Load(data, region)))
            {
                var key = EntryIds.Key(e.Kind, e.Month, e.Day, e.Year, e.Title);
                var id = old.TryGetValue(key, out var kept) ? kept : EntryIds.Make(e.Kind, e.Month, e.Day, e.Year, e.Title);
                rows[id + "|" + key] = $"{id}|{e.Kind}|{e.Month:00}-{e.Day:00}|{e.Year}|{e.Title}";
            }
        var lines = new List<string> { "# id|kind|MM-DD|year|title   (stable identifiers; written by the test DumpIdRegistry, never by hand except to keep an id when a title is corrected)" };
        lines.AddRange(rows.Values);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllLines(file, lines, new System.Text.UTF8Encoding(false));
    }
}

public class NameDumpTests
{
    [Fact]
    public void DumpNames()
    {
        var file = Environment.GetEnvironmentVariable("CHRON_NAMES");
        if (string.IsNullOrEmpty(file)) return;
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        var lines = new List<string>();
        foreach (var region in new[] { "gr", "cy" })
        {
            var store = DayStore.Load(Path.Combine(dir!, "data"), region);
            // only the database that was written by hand: the memories taken from the Megas Synaxaristis are not counted (they are what is compared with it)
            foreach (var (m, d, c) in store.AllCommemorationsByDay().Where(x => !x.C.Sources.Any(s => s.Url?.Contains("synaxarion.gr/gr/sid/", StringComparison.Ordinal) == true)))
            {
                lines.Add($"title|{m:00}-{d:00}|{c.Title}");
                foreach (var n in c.Names) lines.Add($"{m:00}-{d:00}|{n.Name}|{string.Join(",", n.Variants)}|{n.Etymology.Origin}|{n.Etymology.Meaning}");
            }
        }
        File.WriteAllLines(file, lines.Distinct());
    }
}
