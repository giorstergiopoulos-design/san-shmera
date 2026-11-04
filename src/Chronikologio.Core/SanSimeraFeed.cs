using System.Globalization;

namespace Chronikologio.Core;

public enum FeedKind { NameDays, Saint, Event, Birth, Holiday, Fasting }

public sealed record FeedItem(FeedKind Kind, string Text, string Source);

/// <summary>The "Σαν σήμερα" stream for the bars of Opsis News Crawler: plain text items built from the app's own database.</summary>
public static class SanSimeraFeed
{
    private static readonly CultureInfo El = CultureInfo.GetCultureInfo("el-GR");

    public static IReadOnlyList<FeedItem> Build(DayStore store, DateOnly date)
    {
        var day = store.GetDay(date);
        var items = new List<FeedItem>();
        var stamp = date.ToString("d MMMM", El);

        // holidays and the fast first: the bars of a news ticker are most useful with "what kind of day is it"
        foreach (var h in day.HolidayList)
            items.Add(new FeedItem(FeedKind.Holiday, $"{stamp}: {Holidays.Line(h)}", "Χρονικολόγιο"));
        if (day.Fasting is not null)
            items.Add(new FeedItem(FeedKind.Fasting, $"{stamp}: {day.Fasting}", "Χρονικολόγιο"));

        var names = day.Names.Select(n => n.Name).Distinct().ToList();
        if (names.Count > 0)
            items.Add(new FeedItem(FeedKind.NameDays, $"Γιορτάζουν σήμερα, {stamp}: {string.Join(", ", names)} {OldCalendar.Note(date)}".TrimEnd(), "Χρονικολόγιο"));

        foreach (var c in day.Commemorations.OrderByDescending(c => c.IsMajorFeast))
            items.Add(new FeedItem(FeedKind.Saint, $"{stamp}: {c.Title}", "Χρονικολόγιο"));

        foreach (var e in day.Events)
            items.Add(new FeedItem(FeedKind.Event, e.Year is { } y ? $"Σαν σήμερα, {stamp} {y}: {e.Title}" : $"Σαν σήμερα, {stamp}: {e.Title}", "Χρονικολόγιο"));

        if (day.People.Count > 0)
            items.Add(new FeedItem(FeedKind.Birth, $"Γεννήθηκαν σαν σήμερα, {stamp}: " + string.Join(", ", day.People.Take(4).Select(p => $"{p.Name} ({p.YearText})")), "Χρονικολόγιο"));

        return items;
    }
}
