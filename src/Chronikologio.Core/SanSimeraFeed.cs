using System.Globalization;

namespace Chronikologio.Core;

public enum FeedKind { NameDays, Saint, Event, Birth, Holiday, Fasting, Death }

public sealed record FeedItem(FeedKind Kind, string Text, string Source);

/// <summary>The "Σαν σήμερα" stream for the bars of Opsis News Crawler: plain text items built from the app's own database.</summary>
public static class SanSimeraFeed
{


    /// <summary>
    /// The lines for the bars of the News Crawler: the same as <see cref="Build"/> but short enough for a ticker. At most 3 saints, 4 events (the history of the
    /// chosen country first, two lines at most, then the world) and lines of at most 200 characters (the line of the names is never cut: every name that
    /// celebrates must be there).
    /// </summary>
    public static IReadOnlyList<FeedItem> BuildForTicker(DayStore store, DateOnly date) => Build(store, date, maxSaints: 3, maxEvents: 4, maxDomesticEvents: 2, maxLine: 200);

    public static IReadOnlyList<FeedItem> Build(DayStore store, DateOnly date, int maxSaints = int.MaxValue, int maxEvents = int.MaxValue, int maxDomesticEvents = int.MaxValue, int maxLine = int.MaxValue)
    {
        var day = store.GetDay(date);
        var items = new List<FeedItem>();
        var stamp = date.ToString("d MMMM", Loc.Culture);

        // holidays and the fast first: the bars of a news ticker are most useful with "what kind of day is it"
        foreach (var h in day.HolidayList)
            items.Add(new FeedItem(FeedKind.Holiday, $"{stamp}: {Holidays.Line(h)}", "Χρονικολόγιο"));
        if (day.Fasting is not null)
            items.Add(new FeedItem(FeedKind.Fasting, $"{stamp}: {day.Fasting}", "Χρονικολόγιο"));

        var names = day.Names.Select(n => n.Name).Distinct().ToList();
        if (names.Count > 0)
            items.Add(new FeedItem(FeedKind.NameDays, (string.Format(Loc.T("feed.names"), stamp, string.Join(", ", names)) + " " + OldCalendar.Note(date)).TrimEnd(), "Χρονικολόγιο"));

        foreach (var c in day.Commemorations.OrderByDescending(c => c.IsMajorFeast).Take(maxSaints))
            items.Add(new FeedItem(FeedKind.Saint, $"{stamp}: {c.Title}", "Χρονικολόγιο"));

        var domestic = day.Events.Where(e => Countries.IsDomestic(store.Region, e)).Take(maxDomesticEvents).ToList();
        var abroad = day.Events.Where(e => !Countries.IsDomestic(store.Region, e)).Take(Math.Max(0, maxEvents - domestic.Count));
        foreach (var e in domestic.Concat(abroad).Take(maxEvents))
            items.Add(new FeedItem(FeedKind.Event, e.Year is { } y ? string.Format(Loc.T("feed.event.year"), stamp, y.ToString(CultureInfo.InvariantCulture), e.Title) : string.Format(Loc.T("feed.event"), stamp, e.Title), "Χρονικολόγιο"));

        if (day.People.Count > 0)
            items.Add(new FeedItem(FeedKind.Birth, string.Format(Loc.T("feed.birth"), stamp, string.Join(", ", day.People.Take(4).Select(p => $"{p.Name} ({p.YearText})"))), "Χρονικολόγιο"));

        if (day.DeathList.Count > 0)
            items.Add(new FeedItem(FeedKind.Death, string.Format(Loc.T("feed.death"), stamp, string.Join(", ", day.DeathList.Take(4).Select(p => $"{p.Name} ({p.YearText})"))), "Χρονικολόγιο"));

        return maxLine == int.MaxValue ? items : items.Select(i => i.Kind == FeedKind.NameDays ? i : i with { Text = Cut(i.Text, maxLine) }).ToList();
    }

    private static string Cut(string text, int max)
    {
        if (text.Length <= max) return text;
        var at = text.LastIndexOf(" ", max - 1, StringComparison.Ordinal);
        return text[..(at > max / 2 ? at : max - 1)].TrimEnd(',', ';', ' ', '.') + "…";
    }
}
