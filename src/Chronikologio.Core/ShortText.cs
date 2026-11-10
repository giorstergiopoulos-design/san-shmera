using System.Globalization;
using System.Text;

namespace Chronikologio.Core;

/// <summary>The few lines shown in the small text frame under the calendar: who celebrates, one short life, one event of the day.</summary>
public static class ShortText
{
    private const int MaxSummary = 200;

    public static string Build(DayView day, string region = "gr")
    {
        if (day.Commemorations.Count == 0 && day.Events.Count == 0 && day.People.Count == 0)
            return day.Fasting is null
                ? Loc.T("short.noentries")
                : day.Fasting + "\n\n" + Loc.T("short.noothers");

        var sb = new StringBuilder();
        foreach (var h in day.HolidayList) sb.Append(Holidays.Line(h)).Append(".\n");
        if (day.HolidayList.Count > 0) sb.Append('\n');
        if (day.Fasting is not null) sb.Append(day.Fasting).Append("\n\n");

        var names = day.Names.Select(n => n.Name).Distinct().ToList();
        if (names.Count > 0)
            sb.Append(Loc.T("short.names")).Append(string.Join(", ", names)).Append(". ").Append(OldCalendar.Note(day.Month, day.Day));

        var main = day.Commemorations.OrderByDescending(c => c.IsMajorFeast).FirstOrDefault();
        if (main is not null)
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append(main.Title).Append(". ").Append(Shorten(main.Summary, MaxSummary));
        }

        if (day.People.Count > 0)
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append(Loc.T("short.births")).Append(string.Join(", ", day.People.Take(3).Select(p => $"{p.Name} ({p.YearText})"))).Append('.');
        }

        // One Greek event first, then one of world history (each only if the day has one).
        foreach (var ev in new[] { day.Events.FirstOrDefault(e => Countries.IsDomestic(region, e)), day.Events.FirstOrDefault(e => !Countries.IsDomestic(region, e)) })
        {
            if (ev is null) continue;
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append(Countries.IsDomestic(region, ev) ? Loc.TR("short.history.gr", region) : Loc.T("short.history.world"));
            if (ev.Year is { } y) sb.Append(' ').Append(y.ToString(CultureInfo.InvariantCulture));
            sb.Append(": ").Append(ev.Title).Append('.');
        }
        return sb.ToString();
    }

    /// <summary>"Who celebrates" line for a day: the names, or an empty string.</summary>
    public static string NamesLine(DayView day)
    {
        var names = day.Names.Select(n => n.Name).Distinct().ToList();
        return names.Count == 0 ? "" : Loc.T("short.names") + string.Join(", ", names) + ". " + OldCalendar.Note(day.Month, day.Day);
    }

    /// <summary>The saint(s) of the day: the main title with its short life, then the titles of the others.</summary>
    public static string SaintText(DayView day)
    {
        if (day.Commemorations.Count == 0) return "";
        var ordered = day.Commemorations.OrderByDescending(c => c.IsMajorFeast).ToList();
        var sb = new StringBuilder();
        var main = ordered[0];
        sb.Append(main.Title).Append(". ").Append(Shorten(main.Summary, 260));
        foreach (var other in ordered.Skip(1)) sb.Append("\n\n").Append(other.Title);
        return sb.ToString();
    }

    /// <summary>A list of events, one per paragraph, "year — title. summary".</summary>
    public static string EventsText(IEnumerable<HistoricalEvent> events, int take = 3) =>
        string.Join("\n\n", events.Take(take).Select(e =>
            (e.Year is { } y ? y.ToString(CultureInfo.InvariantCulture) + " — " : "") + e.Title + ". " + Shorten(e.Summary, 160)));

    public static string BirthsText(DayView day, int take = 5) =>
        string.Join("\n", day.People.Take(take).Select(p => $"{p.YearText} — {p.Name}, {p.Description}"));

    /// <summary>The births of the day and, under them, the deaths (each part only when the day has it).</summary>
    public static string PeopleText(DayView day, int take = 4)
    {
        var sb = new StringBuilder();
        if (day.People.Count > 0) sb.Append(string.Join("\n", day.People.Take(take).Select(p => $"{p.YearText} — {p.Name}, {p.Description}")));
        if (day.DeathList.Count > 0)
        {
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append(Loc.T("details.deaths").Trim()).Append('\n');
            sb.Append(string.Join("\n", day.DeathList.Take(take).Select(p => $"{p.YearText} — {p.Name}, {p.Description}")));
        }
        return sb.ToString();
    }

    /// <summary>Whole sentences up to the limit; a single longer sentence is cut at a word and closed with "…".</summary>
    internal static string Shorten(string text, int max)
    {
        text = text.Trim();
        if (text.Length <= max) return text;

        var result = new StringBuilder();
        foreach (var sentence in text.Split(". ", StringSplitOptions.RemoveEmptyEntries))
        {
            var piece = sentence.TrimEnd('.') + ".";
            if (result.Length + piece.Length + 1 > max) break;
            if (result.Length > 0) result.Append(' ');
            result.Append(piece);
        }
        if (result.Length > 0) return result.ToString();

        var cut = text.LastIndexOf(' ', max - 1);
        return text[..(cut > 0 ? cut : max)].TrimEnd(',', ';', ' ') + "…";
    }
}
