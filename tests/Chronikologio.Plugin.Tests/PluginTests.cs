using Chronikologio.OpsisPlugin;
using OpsisNewsCrawler.Plugin.Abstractions;
using Xunit;

namespace Chronikologio.Plugin.Tests;

public class PluginTests
{
    [Fact]
    public void StartRegistersTheSanSimeraFeedAndStopRemovesIt()
    {
        var plugin = new ChronikologioPlugin();
        plugin.Start();
        try
        {
            var feed = PluginFeeds.Find(ChronikologioPlugin.FeedKey);
            Assert.NotNull(feed);
            Assert.Equal("Σαν σήμερα", feed!.TitleEl);
            Assert.NotNull(feed.GetItems());
        }
        finally { plugin.Stop(); }
        Assert.Null(PluginFeeds.Find(ChronikologioPlugin.FeedKey));
    }

    [Fact]
    public void OffersACalendarWidgetThatReplacesTheBuiltInOne()
    {
        var widget = Assert.Single(((IPluginWidgetProvider)new ChronikologioPlugin()).GetWidgets());
        Assert.Equal("calendar", widget.ReplacesBuiltIn);
        using var control = widget.CreateContent();
        Assert.NotNull(control);
    }

    [Fact]
    public void PluginAssemblyNameMatchesTheHostSearchPattern() =>
        Assert.EndsWith("Plugin", typeof(ChronikologioPlugin).Assembly.GetName().Name);
}
