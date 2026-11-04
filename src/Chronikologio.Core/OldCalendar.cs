namespace Chronikologio.Core;

/// <summary>
/// The date of the old (Julian) calendar that falls on a date of the revised calendar, for the churches and the people who keep the old calendar.
/// The feast of a saint is celebrated on the same number of the month in both calendars, so a feast of 6 December (revised) is celebrated by the
/// old calendar on the day that is 6 December there, which is 19 December of the civil calendar. The difference is 13 days from 1 March 1900 to 28 February 2100.
/// </summary>
public static class OldCalendar
{
    private static readonly DateOnly From = new(1900, 3, 1);
    private static readonly DateOnly To = new(2100, 2, 28);

    /// <summary>The civil date on which the old calendar celebrates the feast of this date, or null outside the years the 13-day difference holds.</summary>
    public static DateOnly? CelebratedOn(DateOnly date) => date < From || date > To ? null : date.AddDays(13);

    /// <summary>The note for a month and day of the calendar of any year (the difference is the same 13 days), e.g. for the short texts that carry no year.</summary>
    public static string Note(int month, int day) => Note(new DateOnly(2000, month, day));

    /// <summary>"(παλαιό ημερολόγιο: 19 Δεκεμβρίου)" in the interface language, or an empty string.</summary>
    public static string Note(DateOnly date) =>
        CelebratedOn(date) is { } old ? $"({Loc.T("old.calendar")}: {old.ToString("d MMMM", Loc.Culture)})" : "";
}
