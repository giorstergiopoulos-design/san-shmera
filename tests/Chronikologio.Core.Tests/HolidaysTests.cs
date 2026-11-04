using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public class HolidaysTests
{
    private static readonly DayStore Gr = DayStore.Load(FindData(), "gr");
    private static readonly DayStore Cy = DayStore.Load(FindData(), "cy");

    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    [Fact]
    public void GreekPublicHolidaysOf2026()
    {
        // Orthodox Easter 2026 is 12 April
        var days = Holidays.ForYear(2026, "gr").Where(h => h.Kind is HolidayKind.Public or HolidayKind.National).Select(h => h.Date).ToHashSet();
        foreach (var d in new[] { new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 6), new DateOnly(2026, 2, 23), new DateOnly(2026, 3, 25), new DateOnly(2026, 4, 10),
                                  new DateOnly(2026, 4, 12), new DateOnly(2026, 4, 13), new DateOnly(2026, 5, 1), new DateOnly(2026, 6, 1), new DateOnly(2026, 8, 15),
                                  new DateOnly(2026, 10, 28), new DateOnly(2026, 12, 25), new DateOnly(2026, 12, 26) })
            Assert.Contains(d, days);
    }

    [Fact]
    public void CyprusHasItsOwnHolidays()
    {
        var cy = Holidays.ForYear(2026, "cy");
        Assert.Contains(cy, h => h.Date == new DateOnly(2026, 10, 1) && h.Kind == HolidayKind.National);
        Assert.Contains(cy, h => h.Date == new DateOnly(2026, 4, 1));
        Assert.Contains(cy, h => h.Date == new DateOnly(2026, 4, 14));   // Easter Tuesday
        Assert.Contains(cy, h => h.Name.StartsWith("Κατακλυσμός") && h.Date == new DateOnly(2026, 6, 1));
        Assert.DoesNotContain(Holidays.ForYear(2026, "gr"), h => h.Date == new DateOnly(2026, 10, 1));
    }

    [Fact]
    public void LocalHolidaysOfThePatronSaintsCarryTheirCity()
    {
        var local = Holidays.On(new DateOnly(2026, 10, 26), "gr").Single(h => h.Kind == HolidayKind.Local);
        Assert.Equal("Θεσσαλονίκη", local.Place);
        Assert.Contains(Holidays.On(new DateOnly(2026, 11, 30), "gr"), h => h.Place == "Πάτρα");
        Assert.Contains(Holidays.On(new DateOnly(2026, 10, 17), "cy"), h => h.Place == "Λάρνακα");
        Assert.DoesNotContain(Holidays.On(new DateOnly(2026, 10, 26), "cy"), h => h.Kind == HolidayKind.Local);
    }

    [Fact]
    public void DaysOffAreNotTheDaysOfMemoryOrLocalHolidays()
    {
        Assert.True(Holidays.IsDayOff(new DateOnly(2026, 3, 25), "gr"));
        Assert.False(Holidays.IsDayOff(new DateOnly(2026, 11, 17), "gr"));   // Polytechnic: school holiday only
        Assert.False(Holidays.IsDayOff(new DateOnly(2026, 10, 26), "gr"));   // local
    }

    [Fact]
    public void ADayAskedWithItsYearCarriesItsHolidaysAndTheTextShowsThem()
    {
        var day = Gr.GetDay(new DateOnly(2026, 10, 28));
        Assert.NotEmpty(day.HolidayList);
        Assert.Contains("Όχι", ShortText.Build(day));
        Assert.Contains("Θεσσαλονίκη", ShortText.Build(Gr.GetDay(new DateOnly(2026, 10, 26))));
        Assert.Empty(Gr.GetDay(10, 28).HolidayList);   // no year, no movable or yearly information
    }

    [Fact]
    public void FastingPeriodsFollowEaster()
    {
        var periods = Fasting.PeriodsFor(2026);
        var lent = periods.Single(p => p.Name.StartsWith("Μεγάλη Τεσσαρακοστή"));
        Assert.Equal(new DateOnly(2026, 2, 23), lent.Start);
        Assert.Equal(new DateOnly(2026, 4, 11), lent.End);
        Assert.Contains(periods, p => p.Name.StartsWith("Νηστεία των Αγίων Αποστόλων") && p.Start == new DateOnly(2026, 6, 8) && p.End == new DateOnly(2026, 6, 28));
        Assert.Contains(periods, p => p.Name.StartsWith("Νηστεία των Χριστουγέννων") && p.Start == new DateOnly(2026, 11, 15));
        Assert.Empty(Fasting.PeriodsFor(1800));
    }

    [Fact]
    public void ApostlesFastIsMissingInYearsWithALateEaster()
    {
        // Easter 2024 was 5 May: the fast started on 1 July? No: Pentecost + 8 days is 1 July, after the end of the fast on 28 June.
        Assert.DoesNotContain(Fasting.PeriodsFor(2024), p => p.Name.StartsWith("Νηστεία των Αγίων Αποστόλων"));
    }
}
