using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public class ShortTextTests
{
    private static readonly DayStore Store = DayStore.Load(FindData());

    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    [Fact]
    public void EmptyDayExplainsThereAreNoEntries()
    {
        // An empty data folder: every day is empty, whatever the real database holds.
        var dir = Path.Combine(Path.GetTempPath(), "chron-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try { Assert.Contains("Δεν υπάρχουν", ShortText.Build(DayStore.Load(dir).GetDay(1, 13))); }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ALeapDayEventDoesNotBreakOtherYears()
    {
        // 29 February has an entry; asking for 23 April of a non-leap year must still work (it once threw).
        Assert.NotNull(Store.GetDay(new DateOnly(2023, 4, 23)));
        Assert.NotEmpty(Store.GetDay(2, 29).Events);
    }

    [Fact]
    public void DayWithSaintShowsNamesSaintAndEvent()
    {
        var text = ShortText.Build(Store.GetDay(11, 17));
        Assert.Contains("Σαν σήμερα στην ελληνική ιστορία", text);   // which Greek event is named depends on the data of the day

        var nov9 = ShortText.Build(Store.GetDay(11, 9));
        Assert.StartsWith("Γιορτάζουν: Νεκτάριος", nov9);
        Assert.Contains("Άγιος Νεκτάριος", nov9);
    }

    [Fact]
    public void EveryDayStaysShort()
    {
        for (var m = 1; m <= 12; m++)
            for (var d = 1; d <= DateTime.DaysInMonth(2025, m); d++)
                Assert.True(ShortText.Build(Store.GetDay(m, d)).Length <= 700, $"{d}/{m}");
    }

    [Fact]
    public void ShortenKeepsWholeSentences()
    {
        var s = ShortText.Shorten("Πρώτη πρόταση. Δεύτερη πρόταση που είναι πολύ μεγαλύτερη από την πρώτη και δεν χωράει.", 40);
        Assert.Equal("Πρώτη πρόταση.", s);
    }

    [Fact]
    public void ShortenCutsAtAWordWhenOneSentenceIsTooLong()
    {
        var s = ShortText.Shorten("Μία πολύ μεγάλη πρόταση χωρίς καμία τελεία που συνεχίζεται και συνεχίζεται", 30);
        Assert.EndsWith("…", s);
        Assert.True(s.Length <= 31);
    }
}
