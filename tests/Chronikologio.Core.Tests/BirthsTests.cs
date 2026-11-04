using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public class BirthsTests
{
    private static readonly DayStore Store = DayStore.Load(FindData());

    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    [Fact]
    public void BirthsFileLoadsWithoutErrors() => Assert.Empty(Store.Errors);

    [Fact]
    public void MostDaysOfTheYearHaveAPersonBornOnThem()
    {
        Assert.True(Store.BirthCount >= 300, $"only {Store.BirthCount} persons");
        var missing = new List<string>();
        for (var m = 1; m <= 12; m++)
            for (var d = 1; d <= DateTime.DaysInMonth(2024, m); d++)
                if (Store.GetDay(m, d).People.Count == 0) missing.Add($"{d:00}/{m:00}");
        Assert.True(Store.BirthDayCount >= 300, $"{Store.BirthDayCount} days covered; missing: {string.Join(' ', missing)}");
    }

    [Fact]
    public void GreekPersonsComeFirst()
    {
        var people = Store.GetDay(10, 13).People;            // Nana Mouskouri and Pyrros Dimas (both 13 October)
        Assert.True(people[0].IsGreek);
        Assert.True(people.TakeWhile(p => p.IsGreek).Count() >= 2);

        var feb12 = Store.GetDay(2, 12).People;              // Costa-Gavras (Greek) before Darwin and Lincoln (1809)
        Assert.Equal("Κώστας Γαβράς", feb12[0].Name);
    }

    [Fact]
    public void EveryDayWithBirthsHasGreekFirstThenByYear()
    {
        for (var m = 1; m <= 12; m++)
            for (var d = 1; d <= DateTime.DaysInMonth(2024, m); d++)
            {
                var people = Store.GetDay(m, d).People;
                var seenWorld = false;
                foreach (var p in people)
                {
                    if (!p.IsGreek) seenWorld = true;
                    else Assert.False(seenWorld, $"{d}/{m}: a Greek person after a non-Greek one");
                }
            }
    }

    [Fact]
    public void YearsAreSensible()
    {
        foreach (var p in Enumerable.Range(1, 12).SelectMany(m => Enumerable.Range(1, DateTime.DaysInMonth(2024, m)).SelectMany(d => Store.GetDay(m, d).People)))
        {
            Assert.InRange(p.Year, -700, 2010);
            Assert.False(string.IsNullOrWhiteSpace(p.Name));
            Assert.False(string.IsNullOrWhiteSpace(p.Description));
        }
    }

    [Fact]
    public void ShortTextAndFeedMentionTheBirths()
    {
        var day = new DateOnly(2026, 3, 14);
        Assert.Contains("Άλμπερτ Αϊνστάιν (1879)", ShortText.Build(Store.GetDay(day)));
        Assert.Contains(SanSimeraFeed.Build(Store, day), i => i.Kind == FeedKind.Birth && i.Text.Contains("Αϊνστάιν"));
    }

    [Fact]
    public void BcYearsAreWrittenInGreek() =>
        Assert.Equal("100 π.Χ.", Store.GetDay(7, 12).People.Single(p => p.Name.Contains("Καίσαρας")).YearText);
}
