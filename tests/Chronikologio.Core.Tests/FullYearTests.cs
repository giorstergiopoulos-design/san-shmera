using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public class FullYearTests
{
    private static readonly DayStore Store = DayStore.Load(FindData());

    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    private static List<string> DaysWhere(Func<DayView, bool> missing)
    {
        var days = new List<string>();
        for (var m = 1; m <= 12; m++)
            for (var d = 1; d <= DateTime.DaysInMonth(2024, m); d++)
                if (missing(Store.GetDay(m, d))) days.Add($"{d:00}/{m:00}");
        return days;
    }

    [Fact]
    public void NoLoadErrors() => Assert.Empty(Store.Errors);

    [Fact]
    public void AlmostEveryDayHasASaint()
    {
        var missing = DaysWhere(v => v.Commemorations.Count == 0);
        Assert.True(missing.Count <= 15, $"{missing.Count} days without a commemoration: {string.Join(' ', missing)}");
    }

    [Fact]
    public void AlmostEveryDayHasAnEvent()
    {
        var missing = DaysWhere(v => v.Events.Count == 0);
        Assert.True(missing.Count <= 15, $"{missing.Count} days without an event: {string.Join(' ', missing)}");
    }

    [Fact]
    public void MostDaysHavePeopleBornOnThem()
    {
        var missing = DaysWhere(v => v.People.Count == 0);
        Assert.True(missing.Count <= 25, $"{missing.Count} days without births: {string.Join(' ', missing)}");
    }

    [Fact]
    public void SaintsFromTheTsvFilesAreSearchableByName()
    {
        Assert.Contains(Store.FindName("Κοσμάς"), h => h.Month == 8 && h.Day == 24);
        Assert.Contains(Store.FindName("Φανούριος"), h => h.Month == 8 && h.Day == 27);
        Assert.Contains(Store.FindName("Ματθαίος"), h => h.Month == 11 && h.Day == 16);
    }

    [Fact]
    public void TheEventsFileGivesAnEventToItsDays()
    {
        var marathon = Store.GetDay(10, 28).Events;   // the Oxi day (the date of Marathon is only traditional and is no longer listed)
        Assert.Contains(marathon, e => e.Year == 1940 && e.IsGreek);
        Assert.True(marathon[0].IsGreek);
    }
}
