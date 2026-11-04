using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public class FastingTests
{
    [Theory]
    [InlineData(2026, 2, 23, "Μεγάλη Τεσσαρακοστή")]        // Kathara Deftera
    [InlineData(2026, 3, 20, "Μεγάλη Τεσσαρακοστή")]
    [InlineData(2026, 4, 3, "Μεγάλη Τεσσαρακοστή")]         // Friday before Lazarus Saturday
    [InlineData(2026, 4, 11, "Μεγάλης Εβδομάδας")]          // Holy Saturday... Lazarus Saturday is 4 Apr
    [InlineData(2026, 4, 10, "Αυστηρή νηστεία: Μεγάλη Παρασκευή")]
    [InlineData(2026, 4, 13, "Διακαινησίμου")]
    [InlineData(2026, 6, 15, "Αγίων Αποστόλων")]
    [InlineData(2026, 8, 5, "Κοιμήσεως")]
    [InlineData(2026, 11, 20, "Χριστουγέννων")]
    [InlineData(2026, 12, 29, "Δωδεκαήμερο")]
    [InlineData(2027, 1, 5, "παραμονή των Θεοφανίων")]
    [InlineData(2026, 8, 29, "Αποτομή")]
    [InlineData(2026, 9, 14, "Ύψωση")]
    [InlineData(2026, 5, 31, "Πεντηκοστής")]                 // Pentecost Sunday
    public void FastingRuleMatchesTheCalendar(int y, int m, int d, string expected) =>
        Assert.Contains(expected, Fasting.For(new DateOnly(y, m, d)));

    [Fact]
    public void ApostlesFastIsSkippedWhenAllSaintsIsLate()
    {
        // Easter 2024 was 5 May, so All Saints was 30 June and the fast would start after 28 June.
        Assert.Null(Fasting.For(new DateOnly(2024, 6, 20)));   // an ordinary Thursday
        Assert.Contains("Αγίων Αποστόλων", Fasting.For(new DateOnly(2026, 6, 8)));
    }

    [Fact]
    public void OrdinaryWednesdaysAndFridaysFastAndOtherDaysDoNot()
    {
        Assert.Equal("Νηστεία της Τετάρτης.", Fasting.For(new DateOnly(2026, 10, 7)));
        Assert.Equal("Νηστεία της Παρασκευής.", Fasting.For(new DateOnly(2026, 10, 9)));
        Assert.Null(Fasting.For(new DateOnly(2026, 10, 8)));
    }

    [Fact]
    public void WidgetTextCarriesTheFastingNote()
    {
        var store = DayStore.Load(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data"));
        Assert.Contains("Νηστεία των Χριστουγέννων", ShortText.Build(store.GetDay(new DateOnly(2026, 11, 20))));
    }
}
