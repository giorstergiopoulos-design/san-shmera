using System.Reflection;
using System.Runtime.Loader;
using Chronikologio.UI;
using OpsisNewsCrawler.Plugin.Abstractions;
using Xunit;

namespace Chronikologio.Plugin.Tests;

/// <summary>
/// Loads the plugin the way Opsis does (PluginHost): a context of its own per plugin folder, the contract assembly shared with the host,
/// every type of the DLL read with GetTypes (which throws when the host's contract lacks a type the plugin uses), the first class that implements
/// IOpsisNewsCrawlerPlugin created with its parameterless constructor.
/// </summary>
[Collection("theme")]
public class HostLoadingTests
{
    private sealed class PluginContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        public PluginContext(string dll) : base(isCollectible: true) => _resolver = new AssemblyDependencyResolver(dll);

        protected override Assembly? Load(AssemblyName name)
        {
            // the contract assembly is the host's instance: never load a second copy
            if (name.Name == "OpsisNewsCrawler.Plugin.Abstractions") return null;
            var path = _resolver.ResolveAssemblyToPath(name);
            return path is null ? null : LoadFromAssemblyPath(path);
        }
    }

    private static string PluginDll() => Path.Combine(AppContext.BaseDirectory, "Chronikologio.OpsisPlugin.dll");

    [Fact]
    public void TheHostCanReadEveryTypeOfThePluginAndCreateIt()
    {
        var context = new PluginContext(PluginDll());
        try
        {
            var assembly = context.LoadFromAssemblyPath(PluginDll());
            var types = assembly.GetTypes();   // would throw ReflectionTypeLoadException if the contract lacked a type
            var type = Assert.Single(types, t => !t.IsAbstract && typeof(IOpsisNewsCrawlerPlugin).IsAssignableFrom(t));
            Assert.True(typeof(IPluginWidgetProvider).IsAssignableFrom(type));
            var plugin = Assert.IsAssignableFrom<IOpsisNewsCrawlerPlugin>(Activator.CreateInstance(type));
            Assert.Equal("chronikologio", plugin.Id);
            Assert.True(Version.TryParse(plugin.Version, out _), plugin.Version);   // Opsis compares versions to keep the newer copy
        }
        finally { context.Unload(); }
    }

    [Fact]
    public void AfterStartTheCrawlerFindsTheFeedAndTheDesktopFindsTheWidgetThatReplacesTheCalendar()
    {
        var context = new PluginContext(PluginDll());
        try
        {
            var assembly = context.LoadFromAssemblyPath(PluginDll());
            var type = assembly.GetTypes().Single(t => !t.IsAbstract && typeof(IOpsisNewsCrawlerPlugin).IsAssignableFrom(t));
            var plugin = (IOpsisNewsCrawlerPlugin)Activator.CreateInstance(type)!;

            // the order the host uses: language and theme, then Start
            plugin.SetLanguage("el");
            plugin.SetTheme(true, unchecked((int)0xFF18181C), unchecked((int)0xFF23232A), unchecked((int)0xFF3A3A44), unchecked((int)0xFFEDEDF2), unchecked((int)0xFF9A9AA8), unchecked((int)0xFF00BCD4), 1.0);
            plugin.Start();

            var feed = PluginFeeds.Find("san-simera");
            Assert.NotNull(feed);
            Assert.Equal("Σαν σήμερα", feed!.TitleEl);
            var items = feed.GetItems();
            Assert.NotEmpty(items);
            Assert.All(items, i => Assert.False(string.IsNullOrWhiteSpace(i.Text)));

            var widget = Assert.Single(((IPluginWidgetProvider)plugin).GetWidgets());
            Assert.Equal("calendar", widget.ReplacesBuiltIn);
            using var content = widget.CreateContent();
            Assert.Equal("Chronikologio.UI.CalendarPanel", content.GetType().FullName);   // a copy of the UI library inside the plugin's own context, not the test's

            var about = plugin.GetAboutInfo();
            Assert.False(string.IsNullOrWhiteSpace(about.DescriptionEl));

            plugin.Stop();
            Assert.Null(PluginFeeds.Find("san-simera"));
        }
        finally { context.Unload(); }
    }

    [Fact]
    public void TheFeedCarriesTheKindOfTheDayAndTheDayItself()
    {
        var store = Chronikologio.Core.DayStore.Load(Path.Combine(AppContext.BaseDirectory, "data"));
        var items = Chronikologio.Core.SanSimeraFeed.Build(store, new DateOnly(2026, 10, 28));
        Assert.Contains(items, i => i.Kind == Chronikologio.Core.FeedKind.Holiday && i.Text.Contains("Όχι"));
        Assert.Contains(items, i => i.Kind == Chronikologio.Core.FeedKind.NameDays && i.Text.Contains("παλαιό ημερολόγιο"));
        Assert.Contains(items, i => i.Kind == Chronikologio.Core.FeedKind.Event);
    }
}
