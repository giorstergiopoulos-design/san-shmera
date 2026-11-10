using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

/// <summary>The checks that keep the data files in shape (what a JSON schema would say), run for both calendars.</summary>
public class SchemaTests
{
    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    [Fact]
    public void EveryEntryIsWellFormed()
    {
        var problems = new List<string>();
        foreach (var region in new[] { "gr", "cy" })
        {
            var store = DayStore.Load(FindData(), region);
            Assert.Empty(store.Errors);
            for (var i = 0; i < 366; i++)
            {
                var d = new DateOnly(2024, 1, 1).AddDays(i);
                var day = store.GetDay(d.Month, d.Day);
                var at = $"{region} {d:MM-dd}";
                foreach (var c in day.Commemorations)
                {
                    if (string.IsNullOrWhiteSpace(c.Title) || string.IsNullOrWhiteSpace(c.Summary)) problems.Add($"{at}: empty saint text");
                    foreach (var s in c.Sources) if (s.Url is { } u && !u.StartsWith("https://", StringComparison.Ordinal)) problems.Add($"{at}: {c.Title}: url {u}");
                }
                foreach (var e in day.Events)
                {
                    if (string.IsNullOrWhiteSpace(e.Title) || string.IsNullOrWhiteSpace(e.Summary)) problems.Add($"{at}: empty event text {e.Year}");
                    if (e.Year is { } y && (y < -3500 || y > DateTime.Today.Year)) problems.Add($"{at}: {e.Title}: year {y}");
                    if (string.IsNullOrWhiteSpace(e.Region)) problems.Add($"{at}: {e.Title}: no region");
                }
                foreach (var p in day.People.Concat(day.DeathList))
                {
                    if (string.IsNullOrWhiteSpace(p.Name) || string.IsNullOrWhiteSpace(p.Description)) problems.Add($"{at}: empty person {p.Year}");
                    if (p.Year < -3500 || p.Year > DateTime.Today.Year) problems.Add($"{at}: {p.Name}: year {p.Year}");
                }
            }
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems.Distinct().Take(15)) + $"\n({problems.Count})");
    }
}
