using Chronikologio.Core;
using System.Text.RegularExpressions;
using Xunit;

namespace Chronikologio.Core.Tests;

public sealed class EnglishDataTests
{
    private static readonly DayStore Store = DayStore.Load(FindData(), "gr");

    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    private static bool Greek(string s) => Regex.IsMatch(s, "[\u0370-\u03FF\u1F00-\u1FFF]");

    [Fact]
    public void TheEnglishDayHasEnglishTextsAndTheGreekDayKeepsItsOwn()
    {
        var el = Store.GetDayIn(10, 28, "el");
        var en = Store.GetDayIn(10, 28, "en");
        Assert.Contains(el.Events, e => e.Year == 1940 && Greek(e.Title));
        Assert.Contains(en.Events, e => e.Year == 1940 && !Greek(e.Title) && !Greek(e.Summary));
    }

    [Fact]
    public void MostEntriesOfEveryDayAreTranslated()
    {
        int total = 0, greek = 0;
        for (var m = 1; m <= 12; m++)
            for (var d = 1; d <= DateTime.DaysInMonth(2024, m); d++)
            {
                var v = Store.GetDayIn(m, d, "en");
                foreach (var e in v.Events) { total++; if (Greek(e.Title) || Greek(e.Summary)) greek++; }
                foreach (var p in v.People.Concat(v.DeathList)) { total++; if (Greek(p.Name) || Greek(p.Description)) greek++; }
                foreach (var c in v.Commemorations) { total++; if (Greek(c.Title)) greek++; }
            }
        Assert.True(greek <= total * 0.05, $"{greek} of {total} entries are still Greek in the English calendar");
    }

    [Fact]
    public void EnglishLivesAreShownInEnglish()
    {
        var en = Store.GetDayIn(1, 1, "en");
        var life = en.Commemorations.FirstOrDefault(c => c.Details is { Length: > 100 });
        Assert.NotNull(life);
        Assert.False(Greek(life!.Details!), "the life should be English");
    }
}
