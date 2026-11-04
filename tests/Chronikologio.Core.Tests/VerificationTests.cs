using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public class VerificationTests
{
    private static readonly DayStore Store = DayStore.Load(FindData());

    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    [Fact]
    public void MostSaintsAreCrossCheckedAgainstAnIndependentCalendar()
    {
        var all = Store.AllCommemorations.Where(c => c.Sources.All(s => s.Kind != "official-reference")).ToList();
        var reviewed = Store.AllCommemorations.Count(c => c.Status >= Verification.Reviewed);
        Assert.True(reviewed >= 330, $"only {reviewed} commemorations cross-checked");
    }

    [Fact]
    public void EveryEventWithAYearWasCrossCheckedOrIsAKnownException()
    {
        var withYear = Store.AllEvents.Where(e => e.Year is not null).ToList();
        var reviewed = withYear.Count(e => e.Status >= Verification.Reviewed);
        Assert.True(reviewed >= withYear.Count * 0.9, $"{reviewed} of {withYear.Count} events cross-checked");
    }

    [Fact]
    public void ACrossCheckAddsItsSourceAndNeverClaimsMoreThanReviewedWithoutALink()
    {
        foreach (var c in Store.AllCommemorations.Where(c => c.Status == Verification.Reviewed))
            Assert.Contains(c.Sources, s => s.Kind == "calendar-crosscheck");
        foreach (var e in Store.AllEvents.Where(e => e.Status == Verification.Reviewed))
            Assert.Contains(e.Sources, s => s.Kind == "calendar-crosscheck");
        Assert.DoesNotContain(Store.AllCommemorations, c => c.Status == Verification.Verified && c.Sources.Any(s => string.IsNullOrEmpty(s.Url)));
    }

    [Fact]
    public void TheCrossCheckedDatesAreTheOnesTheAppShows()
    {
        var nick = Store.GetDay(12, 6).Commemorations.Single(c => c.Title.StartsWith("Άγιος Νικόλαος"));
        Assert.Equal(Verification.Reviewed, nick.Status);
        Assert.Contains(nick.Sources, s => s.Url?.Contains("orthocal.info") == true);
    }
}
