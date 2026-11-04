using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public class CyprusTests
{
    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    private static readonly DayStore Gr = DayStore.Load(FindData(), "gr");
    private static readonly DayStore Cy = DayStore.Load(FindData(), "cy");

    [Fact]
    public void BothCalendarsLoadWithoutErrors()
    {
        Assert.Empty(Gr.Errors);
        Assert.Empty(Cy.Errors);
        Assert.Equal("cy", Cy.Region);
        Assert.Equal("gr", DayStore.Load(FindData(), "nonsense").Region);
    }

    [Fact]
    public void CypriotSaintsAppearOnlyInTheCyprusCalendar()
    {
        Assert.DoesNotContain(Gr.GetDay(1, 24).Commemorations, c => c.Title.Contains("Νεόφυτος"));
        Assert.Contains(Cy.GetDay(1, 24).Commemorations, c => c.Title.Contains("Νεόφυτος"));
        Assert.Contains(Cy.GetDay(6, 11).Commemorations, c => c.Title.Contains("Θρονική εορτή"));
    }

    [Fact]
    public void TheCommonFeastsAreInBothCalendars()
    {
        Assert.Contains(Cy.GetDay(11, 9).Commemorations, c => c.Title.Contains("Νεκτάριος"));
        Assert.Contains(Gr.GetDay(11, 9).Commemorations, c => c.Title.Contains("Νεκτάριος"));
    }

    [Fact]
    public void CypriotHistoryAndHolidaysAreOnlyInTheCyprusCalendar()
    {
        Assert.DoesNotContain(Gr.GetDay(4, 1).Events, e => e.Title.Contains("ΕΟΚΑ"));
        Assert.Contains(Cy.GetDay(4, 1).Events, e => e.Title.Contains("ΕΟΚΑ"));
        Assert.Contains(Cy.GetDay(10, 1).Events, e => e.Title.Contains("Εθνική εορτή"));
        Assert.Contains(Cy.GetDay(8, 16).Events, e => e.Year == 1960);
    }

    [Fact]
    public void KataklysmosAndEasterTuesdayAreCypriot()
    {
        var easter = Paschalion.OrthodoxEaster(2026);                       // 12 April
        Assert.Contains(Cy.GetDay(easter.AddDays(50)).Commemorations, c => c.Title.StartsWith("Κατακλυσμός"));
        Assert.DoesNotContain(Gr.GetDay(easter.AddDays(50)).Commemorations, c => c.Title.StartsWith("Κατακλυσμός"));
        Assert.Contains(Gr.GetDay(easter.AddDays(50)).Commemorations, c => c.Title == "Του Αγίου Πνεύματος");
        Assert.Contains(Cy.GetDay(easter.AddDays(2)).Commemorations, c => c.Title == "Τρίτη της Διακαινησίμου");
        Assert.DoesNotContain(Gr.GetDay(easter.AddDays(2)).Commemorations, c => c.Title == "Τρίτη της Διακαινησίμου");
    }

    [Fact]
    public void AllSaintsOfCyprusAreOnTheFirstSundayOfOctober()
    {
        // 2026: 1 October is a Thursday, so the first Sunday is 4 October. 2027: 1 October is a Friday, the first Sunday is 3 October.
        Assert.Contains(Cy.GetDay(new DateOnly(2026, 10, 4)).Commemorations, c => c.Title.StartsWith("Σύναξη πάντων των εν Κύπρω"));
        Assert.Contains(Cy.GetDay(new DateOnly(2027, 10, 3)).Commemorations, c => c.Title.StartsWith("Σύναξη πάντων των εν Κύπρω"));
        Assert.DoesNotContain(Cy.GetDay(new DateOnly(2026, 10, 11)).Commemorations, c => c.Title.StartsWith("Σύναξη πάντων των εν Κύπρω"));
        Assert.DoesNotContain(Gr.GetDay(new DateOnly(2026, 10, 4)).Commemorations, c => c.Title.StartsWith("Σύναξη πάντων των εν Κύπρω"));
    }

    [Fact]
    public void EveryCypriotRecordHasSummarySourceAndNameMeaning()
    {
        var cypriot = Cy.AllCommemorations.Where(c => c.Sources.Any(s => s.Citation.Contains("Ιερά Αρχιεπισκοπή Κύπρου"))).ToList();
        Assert.NotEmpty(cypriot);
        foreach (var c in cypriot)
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Summary), c.Title);
            Assert.True(c.Summary.Length <= 330, $"{c.Title}: {c.Summary.Length}");
            Assert.NotEmpty(c.Names);
            Assert.All(c.Names, n => Assert.False(string.IsNullOrWhiteSpace(n.Etymology.Meaning), n.Name));
            Assert.Equal(Verification.Unverified, c.Status);
        }
    }

    [Fact]
    public void NoCypriotRecordCopiesFromTheChurchSite()
    {
        // The terms of use of churchofcyprus.org.cy forbid reproducing its content: no source of ours may claim to be that site's text.
        foreach (var s in Cy.AllCommemorations.SelectMany(c => c.Sources))
            Assert.True(string.IsNullOrEmpty(s.Url) || !s.Url.Contains("churchofcyprus.org.cy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OrderPutsCyprusFirstInTheCyprusCalendarAndTheGreekWorldFirstInGreece()
    {
        var dir = Path.Combine(Path.GetTempPath(), "chron-order-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "births"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "births", "births.tsv"),
                "03-05|1900|Γαλλία|Ξένος|x\n03-05|1950|Ελλάδα|Έλληνας|x\n");
            File.WriteAllText(Path.Combine(dir, "births", "births-cy.tsv"), "03-05|1990|Κύπρος|Κύπριος|x\n");

            var greek = DayStore.Load(dir, "gr").GetDay(3, 5).People.Select(p => p.Name).ToList();
            Assert.Equal(new[] { "Έλληνας", "Ξένος" }, greek);                       // births-cy.tsv is not read

            var cypriot = DayStore.Load(dir, "cy").GetDay(3, 5).People.Select(p => p.Name).ToList();
            Assert.Equal(new[] { "Κύπριος", "Έλληνας", "Ξένος" }, cypriot);          // Cyprus, then Greece, then the world
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void CypriotsBornOnTheDayAreListedInTheCyprusCalendar()
    {
        Assert.Contains(Cy.GetDay(8, 13).People, p => p.Name == "Μακάριος Γ΄");
        Assert.DoesNotContain(Gr.GetDay(8, 13).People, p => p.Name == "Μακάριος Γ΄");
    }
}
