namespace Chronikologio.Core;

/// <summary>
/// The fasting rule of a date in the Greek Orthodox Church: the four great fasts, the strict single days, the fast-free weeks and the ordinary
/// Wednesdays and Fridays. Returns a short Greek note or null. Feast days of the Lord that fall on a Wednesday or Friday are not treated specially.
/// </summary>
public static class Fasting
{
    /// <summary>The fasting periods, the strict single days and the fast-free weeks of a year, in date order. Empty outside 1900–2099.</summary>
    public static IReadOnlyList<FastPeriod> PeriodsFor(int year)
    {
        if (year is < 1900 or > 2099) return Array.Empty<FastPeriod>();
        var easter = Paschalion.OrthodoxEaster(year);
        var list = new List<FastPeriod>
        {
            new(new DateOnly(year, 1, 5), new DateOnly(year, 1, 5), "Παραμονή των Θεοφανίων", Loc.T("fast.strict")),
            new(easter.AddDays(-48), easter.AddDays(-1), "Μεγάλη Τεσσαρακοστή και Μεγάλη Εβδομάδα", Loc.T("fast.lent")),
            new(new DateOnly(year, 8, 1), new DateOnly(year, 8, 14), "Νηστεία της Κοιμήσεως της Θεοτόκου", Loc.T("fast.fish")),
            new(new DateOnly(year, 8, 29), new DateOnly(year, 8, 29), "Αποτομή της κεφαλής του Αγίου Ιωάννη του Προδρόμου", Loc.T("fast.strict")),
            new(new DateOnly(year, 9, 14), new DateOnly(year, 9, 14), "Ύψωση του Τιμίου Σταυρού", Loc.T("fast.strict")),
            new(new DateOnly(year, 11, 15), new DateOnly(year, 12, 24), "Νηστεία των Χριστουγέννων", Loc.T("fast.fish")),
            // fast-free weeks
            new(easter.AddDays(-69), easter.AddDays(-63), "Εβδομάδα του Τελώνου και του Φαρισαίου", Loc.T("fast.free")),
            new(easter.AddDays(-55), easter.AddDays(-49), "Εβδομάδα της Τυρινής", Loc.T("fast.dairy")),
            new(easter, easter.AddDays(6), "Διακαινήσιμος εβδομάδα", Loc.T("fast.free")),
            new(easter.AddDays(49), easter.AddDays(55), "Εβδομάδα της Πεντηκοστής", Loc.T("fast.free")),
            new(new DateOnly(year, 1, 1), new DateOnly(year, 1, 4), "Δωδεκαήμερο των Χριστουγέννων", Loc.T("fast.free")),
            new(new DateOnly(year, 12, 25), new DateOnly(year, 12, 31), "Δωδεκαήμερο των Χριστουγέννων", Loc.T("fast.free"))
        };
        var apostlesStart = easter.AddDays(57);
        var apostlesEnd = new DateOnly(year, 6, 28);
        if (apostlesStart <= apostlesEnd) list.Add(new FastPeriod(apostlesStart, apostlesEnd, "Νηστεία των Αγίων Αποστόλων", Loc.T("fast.fish")));
        return list.OrderBy(p => p.Start).ToList();
    }

    public static string? For(DateOnly date)
    {
        if (date.Year is < 1900 or > 2099) return null;
        var easter = Paschalion.OrthodoxEaster(date.Year);
        var k = date.DayNumber - easter.DayNumber;   // days since Easter Sunday
        var (month, day) = (date.Month, date.Day);

        if (month == 1 && day == 5) return "Αυστηρή νηστεία: παραμονή των Θεοφανίων.";
        if (month == 8 && day == 29) return "Αυστηρή νηστεία: Αποτομή της κεφαλής του Αγίου Ιωάννη του Προδρόμου.";
        if (month == 9 && day == 14) return "Αυστηρή νηστεία: Ύψωση του Τιμίου Σταυρού.";

        if (k == -2) return "Αυστηρή νηστεία: Μεγάλη Παρασκευή.";
        if (k is >= -8 and <= -1) return "Νηστεία της Μεγάλης Εβδομάδας.";
        if (k is >= -48 and <= -9) return "Μεγάλη Τεσσαρακοστή: νηστεία πριν από το Πάσχα.";
        if (k is >= 0 and <= 6) return "Εβδομάδα της Διακαινησίμου: δεν υπάρχει νηστεία.";
        if (k is >= 49 and <= 55) return "Εβδομάδα της Πεντηκοστής: δεν υπάρχει νηστεία.";

        if (k is >= -69 and <= -63) return "Εβδομάδα του Τελώνου και του Φαρισαίου: δεν υπάρχει νηστεία.";
        if (k is >= -55 and <= -49) return "Εβδομάδα της Τυρινής: επιτρέπονται γαλακτοκομικά και αυγά, όχι κρέας.";

        if ((month == 12 && day >= 25) || (month == 1 && day <= 4)) return "Δωδεκαήμερο των Χριστουγέννων: δεν υπάρχει νηστεία.";
        if ((month == 11 && day >= 15) || (month == 12 && day <= 24)) return "Νηστεία των Χριστουγέννων.";
        if (month == 8 && day <= 14) return "Νηστεία της Κοιμήσεως της Θεοτόκου.";
        if (k >= 57 && (month < 6 || (month == 6 && day <= 28))) return "Νηστεία των Αγίων Αποστόλων.";

        return date.DayOfWeek switch
        {
            DayOfWeek.Wednesday => "Νηστεία της Τετάρτης.",
            DayOfWeek.Friday => "Νηστεία της Παρασκευής.",
            _ => null
        };
    }
}
