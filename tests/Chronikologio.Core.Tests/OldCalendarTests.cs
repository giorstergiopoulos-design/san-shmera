using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public class OldCalendarTests
{
    private static readonly DayStore Store = DayStore.Load(FindData());

    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    [Fact]
    public void ChristmasOfTheOldCalendarIsThe7thOfJanuary()
    {
        Assert.Equal(new DateOnly(2027, 1, 7), OldCalendar.CelebratedOn(new DateOnly(2026, 12, 25)));
        Assert.Equal("(παλαιό ημερολόγιο: 7 Ιανουαρίου)", OldCalendar.Note(new DateOnly(2026, 12, 25)));
    }

    [Fact]
    public void TheDifferenceHoldsOnlyBetween1900And2100()
    {
        Assert.Null(OldCalendar.CelebratedOn(new DateOnly(1899, 12, 6)));
        Assert.Null(OldCalendar.CelebratedOn(new DateOnly(2100, 3, 1)));
        Assert.NotNull(OldCalendar.CelebratedOn(new DateOnly(2100, 2, 28)));
    }

    [Fact]
    public void ANoteExistsForEveryDayOfTheYearIncludingTheLeapDay()
    {
        for (var m = 1; m <= 12; m++)
            for (var d = 1; d <= DateTime.DaysInMonth(2024, m); d++)
                Assert.StartsWith("(παλαιό ημερολόγιο:", OldCalendar.Note(m, d));
    }

    [Fact]
    public void TheShortTextShowsTheOldCalendarDateNextToTheNames()
    {
        var text = ShortText.Build(Store.GetDay(12, 6));
        Assert.Contains("Γιορτάζουν: Νικόλαος", text);
        Assert.Contains("(παλαιό ημερολόγιο: 19 Δεκεμβρίου)", text);
    }
}
