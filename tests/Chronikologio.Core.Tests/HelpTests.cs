using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public class HelpTests
{
    [Fact]
    public void TheChangelogIsNewestFirstAndEveryEntryHasItems()
    {
        Assert.NotEmpty(Changelog.Entries);
        var versions = Changelog.Entries.Select(e => Version.Parse(e.Version)).ToList();
        Assert.Equal(versions.OrderByDescending(v => v), versions);
        Assert.All(Changelog.Entries, e => { Assert.NotEmpty(e.Items); Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", e.Date); });
    }

    [Fact]
    public void TheGuideAndTheMenuTextsExist()
    {
        foreach (var key in new[] { "menu.guide", "menu.changelog", "guide.title", "changelog.title", "guide.text" })
            Assert.NotEqual(key, Loc.T(key));
        Assert.Contains("Ctrl+M", Loc.T("guide.text"));
    }
}
