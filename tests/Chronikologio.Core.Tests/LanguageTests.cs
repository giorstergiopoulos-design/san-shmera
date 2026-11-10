using Chronikologio.Core;
using Xunit;

namespace Chronikologio.Core.Tests;

public class LanguageTests
{
    private static string FindData()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !Directory.Exists(Path.Combine(dir, "data"))) dir = Path.GetDirectoryName(dir);
        return Path.Combine(dir ?? throw new DirectoryNotFoundException("data folder not found"), "data");
    }

    [Fact]
    public void EnglishAndGreekHaveTheSameKeys()
    {
        var el = Loc.KeysOf("el").ToHashSet();
        var en = Loc.KeysOf("en").ToHashSet();
        Assert.Empty(el.Except(en));
        Assert.Empty(en.Except(el));
        foreach (var key in en) Assert.False(string.IsNullOrWhiteSpace(Loc.TIn("en", key)), key);
    }

    [Fact]
    public void TheFormatKeysTakeTheSameArgumentsInBothLanguages()
    {
        foreach (var key in Loc.KeysOf("el").Where(k => k.StartsWith("feed.", StringComparison.Ordinal) || k == "about.format"))
        {
            var args = new object[] { "a", "b", "c" };
            _ = string.Format(Loc.TIn("el", key), args);
            _ = string.Format(Loc.TIn("en", key), args);
            for (var i = 0; i < 3; i++)
                Assert.Equal(Loc.TIn("el", key).Contains("{" + i + "}"), Loc.TIn("en", key).Contains("{" + i + "}"));
        }
    }

    [Fact]
    public void TheLanguageIsTheCountrysUnlessTheUserChoseOne()
    {
        Assert.Equal("el", Countries.Resolve("auto", "gr"));
        Assert.Equal("el", Countries.Resolve(null, "cy"));
        Assert.Equal("en", Countries.Resolve("en", "gr"));
        Assert.Equal("en", Countries.Resolve("en", "cy"));
        Assert.Equal("el", Countries.Resolve("el", "cy"));
        Assert.Equal("el", Countries.Resolve("fr", "gr"));
    }

    [Fact]
    public void TheFeedSpeaksTheLanguageOfTheApp()
    {
        var store = DayStore.Load(FindData(), "gr");
        var day = new DateOnly(2026, 7, 20);
        try
        {
            Assert.True(Loc.Set("en"));
            var en = SanSimeraFeed.Build(store, day);
            Assert.Contains(en, i => i.Kind == FeedKind.Event && i.Text.StartsWith("On this day, 20 July", StringComparison.Ordinal));
            Assert.Contains(en, i => i.Kind == FeedKind.NameDays && i.Text.StartsWith("Celebrating today, 20 July", StringComparison.Ordinal));
            Loc.Set("el");
            var el = SanSimeraFeed.Build(store, day);
            Assert.Contains(el, i => i.Kind == FeedKind.Event && i.Text.StartsWith("Σαν σήμερα, 20 Ιουλίου", StringComparison.Ordinal));
        }
        finally { Loc.Set("el"); }
    }
}
