using Chronikologio.Core;
using Chronikologio.UI;
using OpsisNewsCrawler.Plugin.Abstractions;

namespace Chronikologio.OpsisPlugin;

/// <summary>
/// Opsis plugin: offers the calendar widget (replacing the built-in one) and the "Σαν σήμερα" feed for the bars of the News Crawler.
/// </summary>
public sealed class ChronikologioPlugin : IOpsisNewsCrawlerPlugin, IPluginWidgetProvider
{
    public const string FeedKey = "san-simera";

    private DailySnapshotService? _service;
    private string _language = "el";

    public string Id => "chronikologio";
    public string Name => "Χρονικολόγιο — Timeline Calendar";
    public string Version => typeof(ChronikologioPlugin).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";

    public event Action<string>? NavigationRequested { add { } remove { } }

    // The calendar (Greece or Cyprus) is the one chosen in the stand-alone app's View menu; it is stored in the same settings file.
    // The service reads the data and makes the lines of the day on a background thread, so the bars never wait for the files.
    private DailySnapshotService Service => _service ??= new DailySnapshotService(
        Path.Combine(Path.GetDirectoryName(typeof(ChronikologioPlugin).Assembly.Location) ?? AppContext.BaseDirectory, "data"),
        UiSettings.Load().Region);

    public void Start()
    {
        var service = Service;
        PluginFeeds.Register(new PluginFeed(FeedKey, "Σαν σήμερα", "On this day", BuildFeed));
        // a new snapshot (midnight, another country, another language): register the feed again, which tells the bars to ask for the lines again
        service.Changed += () => PluginFeeds.Register(new PluginFeed(FeedKey, "Σαν σήμερα", "On this day", BuildFeed));
        service.Start();
    }

    public void Stop()
    {
        PluginFeeds.Unregister(FeedKey);
        _service?.Dispose();
        _service = null;
    }

    public void Pause() { }
    public void Resume() { }
    public void Refresh() => _service?.Refresh();
    public void SetMode(string mode) { }
    public void SetPosition(string position) { }
    public void SetLanguage(string code)
    {
        _language = code;
        if (Loc.Set(code)) _service?.LanguageChanged();   // the interface language follows Opsis when the app has that language; otherwise it keeps its own
    }

    public void SetTheme(bool dark, int background, int surface, int border, int text, int textMuted, int accent, double opacity)
    {
        // The widget follows the colours of Opsis; every open calendar listens to the theme and restyles itself.
        ChronikologioTheme.SetFromHost(dark, background, surface, border, text, textMuted, accent);
    }

    public IReadOnlyList<PluginWidget> GetWidgets() => new[]
    {
        new PluginWidget("calendar", "Ημερολόγιο", "Calendar", CreateCalendar, ReplacesBuiltIn: "calendar")
    };

    public IReadOnlyList<PluginNavigationItem> GetNavigationItems() => Array.Empty<PluginNavigationItem>();
    public IReadOnlyList<PluginMenuItem> GetTrayMenuItems() => Array.Empty<PluginMenuItem>();

    public PluginAboutInfo GetAboutInfo() => new(
        Name, Version,
        "Εορτολόγιο, ημερολόγιο και «Σαν σήμερα» με βίους αγίων, ετυμολογίες ονομάτων και ιστορικά γεγονότα. Αντικαθιστά το ενσωματωμένο ημερολόγιο και προσθέτει τη ροή «Σαν σήμερα» στις μπάρες.",
        "Name days, calendar and \"on this day\" with saints' lives, name etymologies and history. Replaces the built-in calendar and adds the \"On this day\" feed to the bars.",
        "Χρονικολόγιο", "—",
        new[]
        {
            new PluginAboutSection("Δεδομένα", "Data",
                "Οι εγγραφές έχουν γραφτεί από την αρχή και δείχνουν ποια κατάσταση επαλήθευσης έχουν. Στο τέλος κάθε κειμένου αναφέρονται οι πηγές.")
        });

    private Control CreateCalendar()
    {
        var panel = new CalendarPanel(Service.Store);
        panel.StoreChanged += store => Service.SetRegion(store.Region);   // the bar feed follows the country chosen with the flag
        return panel;
    }

    // Called by the bars on the UI thread: only reads the ready snapshot (empty for the first moments after Start).
    private IReadOnlyList<PluginFeedItem> BuildFeed()
    {
        var service = _service;
        if (service is null) return Array.Empty<PluginFeedItem>();
        service.CheckDate();
        var snapshot = service.Current;
        return snapshot is null ? Array.Empty<PluginFeedItem>() : snapshot.Items.Select(i => new PluginFeedItem(i.Text)).ToList();
    }
}