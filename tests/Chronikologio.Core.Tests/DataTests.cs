using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public class DataTests
{
    private static string DataRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    private static readonly DayStore Store = DayStore.Load(DataRoot());

    [Fact]
    public void FilesLoadWithoutErrors()
    {
        Assert.Empty(Store.Errors);
        Assert.True(Store.DayCount > 0);
    }

    [Theory]
    [InlineData("Νεκτάριος")]
    [InlineData("νεκταριοσ")]
    [InlineData("ΝΕΚΤΑΡΙΟΣ")]
    [InlineData("Νεκταρία")]
    public void FindNameIgnoresAccentsAndCase(string query)
    {
        var hits = Store.FindName(query);
        Assert.Contains(hits, h => h.Month == 11 && h.Day == 9);
    }

    [Fact]
    public void FoldHandlesFinalSigmaAndAccents() => Assert.Equal("νεκταριοσ", TextNorm.Fold("Νεκτάριος"));

    [Fact]
    public void EveryRecordHasSummaryAndSource()
    {
        foreach (var c in Store.AllCommemorations)
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Summary), c.Title);
            Assert.NotEmpty(c.Sources);
            Assert.NotEmpty(c.Names);
            Assert.All(c.Names, n => Assert.False(string.IsNullOrWhiteSpace(n.Etymology.Meaning), n.Name));
        }
        foreach (var e in Store.AllEvents)
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Summary), e.Title);
            Assert.NotEmpty(e.Sources);
        }
    }

    [Fact]
    public void SummariesStayShort()
    {
        foreach (var s in Store.AllCommemorations.Select(c => c.Summary).Concat(Store.AllEvents.Select(e => e.Summary)))
            Assert.True(s.Length <= 330, $"Summary too long ({s.Length}): {s}");
    }

    [Fact]
    public void NothingIsMarkedVerifiedWithoutAUrl()
    {
        foreach (var c in Store.AllCommemorations.Where(c => c.Status == Verification.Verified))
            Assert.All(c.Sources, s => Assert.False(string.IsNullOrWhiteSpace(s.Url)));
        foreach (var e in Store.AllEvents.Where(e => e.Status == Verification.Verified))
            Assert.All(e.Sources, s => Assert.False(string.IsNullOrWhiteSpace(s.Url)));
    }

    private static bool IsWikipedia(SourceRef s) => s.Url?.Contains("wikipedia.org", StringComparison.OrdinalIgnoreCase) == true;

    [Fact]
    public void WikipediaIsOnlyAPointerAndNeverTheOnlyDocumentation()
    {
        foreach (var sources in Store.AllCommemorations.Select(c => c.Sources).Concat(Store.AllEvents.Select(e => e.Sources)))
        {
            var wiki = sources.Where(IsWikipedia).ToList();
            if (wiki.Count == 0) continue;
            Assert.All(wiki, s => Assert.Equal("encyclopedia-pointer", s.Kind));
            Assert.Contains(sources, s => s.Kind == "official-reference" && !string.IsNullOrEmpty(s.Url) && !IsWikipedia(s));
        }
    }

    [Fact]
    public void EveryOfficialReferenceIsOnAnOfficialHost()
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, "tools", "history", "official-domains.json"))) root = Path.GetDirectoryName(root);
        var policy = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(root!, "tools", "history", "official-domains.json"))).RootElement;
        var patterns = policy.GetProperty("hostPatterns").EnumerateArray().Select(p => new System.Text.RegularExpressions.Regex(p.GetString()!)).ToList();
        var never = policy.GetProperty("neverOfficial").EnumerateArray().Select(p => p.GetString()!).ToList();

        var all = Store.AllCommemorations.SelectMany(c => c.Sources).Concat(Store.AllEvents.SelectMany(e => e.Sources)).Where(s => s.Kind == "official-reference");
        foreach (var s in all)
        {
            var host = new Uri(s.Url!).Host.ToLowerInvariant();
            Assert.DoesNotContain(never, n => host == n || host.EndsWith("." + n));
            Assert.Contains(patterns, p => p.IsMatch(host));
        }
    }

    [Fact]
    public void FeedHasNameDaysSaintAndEvent()
    {
        var items = SanSimeraFeed.Build(Store, new DateOnly(2026, 11, 17));
        Assert.Contains(items, i => i.Kind == FeedKind.Event && i.Text.Contains("1973"));
        var nov9 = SanSimeraFeed.Build(Store, new DateOnly(2026, 11, 9));
        Assert.Contains(nov9, i => i.Kind == FeedKind.NameDays && i.Text.Contains("Νεκτάριος"));
        Assert.Contains(nov9, i => i.Kind == FeedKind.Saint);
    }
}
