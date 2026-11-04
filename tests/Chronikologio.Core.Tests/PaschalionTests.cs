using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public class PaschalionTests
{
    private static readonly DayStore Store = DayStore.Load(FindData());

    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    [Theory]
    [InlineData(2020, 4, 19)]
    [InlineData(2021, 5, 2)]
    [InlineData(2022, 4, 24)]
    [InlineData(2023, 4, 16)]
    [InlineData(2024, 5, 5)]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 12)]
    [InlineData(2027, 5, 2)]
    [InlineData(2028, 4, 16)]
    [InlineData(2029, 4, 8)]
    public void OrthodoxEasterMatchesKnownDates(int year, int month, int day) =>
        Assert.Equal(new DateOnly(year, month, day), Paschalion.OrthodoxEaster(year));

    [Fact]
    public void EasterIsAlwaysASunday()
    {
        for (var y = 1900; y <= 2099; y++)
            Assert.Equal(DayOfWeek.Sunday, Paschalion.OrthodoxEaster(y).DayOfWeek);
    }

    [Fact]
    public void MovableFeastsFallOnTheRightDays()
    {
        var easter = new DateOnly(2026, 4, 12);
        Assert.Contains(Store.GetDay(easter.AddDays(-48)).Commemorations, c => c.Title == "Καθαρά Δευτέρα");   // 23 February
        Assert.Contains(Store.GetDay(easter.AddDays(-7)).Commemorations, c => c.Title == "Κυριακή των Βαΐων");  // 5 April
        Assert.Contains(Store.GetDay(easter).Commemorations, c => c.Title.StartsWith("Κυριακή του Πάσχα"));
        Assert.Contains(Store.GetDay(easter.AddDays(49)).Commemorations, c => c.Title == "Η Πεντηκοστή");        // 31 May
    }

    [Fact]
    public void EasterNamesAreCelebratedOnEaster()
    {
        var names = Store.GetDay(new DateOnly(2026, 4, 12)).Names.Select(n => n.Name).ToList();
        Assert.Contains("Αναστάσιος", names);
        Assert.Contains("Λάμπρος", names);
    }

    [Fact]
    public void SaintGeorgeStaysOn23AprilWhenEasterIsEarlier()
    {
        // Easter 2023 was 16 April, so 23 April is after it.
        Assert.Contains(Store.GetDay(new DateOnly(2023, 4, 23)).Commemorations, c => c.Title.StartsWith("Άγιος Γεώργιος"));
        Assert.DoesNotContain(Store.GetDay(new DateOnly(2023, 4, 17)).Commemorations, c => c.Title.StartsWith("Άγιος Γεώργιος"));
    }

    [Fact]
    public void SaintGeorgeMovesToEasterMondayWhenEasterIsLater()
    {
        // Easter 2024 was 5 May: 23 April fell in Lent, so St George is kept on Easter Monday, 6 May.
        Assert.DoesNotContain(Store.GetDay(new DateOnly(2024, 4, 23)).Commemorations, c => c.Title.StartsWith("Άγιος Γεώργιος"));
        Assert.Contains(Store.GetDay(new DateOnly(2024, 5, 6)).Commemorations, c => c.Title.StartsWith("Άγιος Γεώργιος"));
    }

    [Fact]
    public void YearsOutsideTheSupportedRangeStillWork()
    {
        var day = Store.GetDay(new DateOnly(2150, 11, 9));
        Assert.Contains(day.Names, n => n.Name == "Νεκτάριος");
    }
}
