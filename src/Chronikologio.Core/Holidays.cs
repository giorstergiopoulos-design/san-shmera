namespace Chronikologio.Core;

/// <summary>National: a national feast day (25 March, 28 October). Public: a public holiday (αργία). Remembrance: a day of national memory. Local: the feast of the patron saint of a city, a local holiday.</summary>
public enum HolidayKind { National, Public, Remembrance, Local }

/// <summary>One holiday on one date. Place is set for local holidays (the city); Note says whose saint it is or what the day commemorates.</summary>
public sealed record Holiday(DateOnly Date, HolidayKind Kind, string Name, string? Place = null, string? Note = null);

/// <summary>A fasting period (or a fast-free week) of a year.</summary>
public sealed record FastPeriod(DateOnly Start, DateOnly End, string Name, string Rule);

/// <summary>
/// The national feast days, public holidays, days of memory and local holidays of the patron saints of the calendar of Greece and of Cyprus,
/// with the movable ones counted from the Orthodox Easter. Written from public knowledge and not yet checked against the official lists
/// (the Greek and Cypriot governments publish the public holidays; the local holidays are decided by each municipality).
/// </summary>
public static class Holidays
{
    private sealed record Rule(int? Month, int? Day, int? EasterOffset, HolidayKind Kind, string Name, string[] Regions, string? Place = null, string? Note = null);

    private static readonly string[] Gr = { "gr" };
    private static readonly string[] Cy = { "cy" };
    private static readonly string[] Both = { "gr", "cy" };

    private static Rule Fixed(int m, int d, HolidayKind kind, string name, string[]? regions = null, string? place = null, string? note = null) =>
        new(m, d, null, kind, name, regions ?? Both, place, note);

    private static Rule Movable(int offset, HolidayKind kind, string name, string[]? regions = null, string? note = null) =>
        new(null, null, offset, kind, name, regions ?? Both, null, note);

    private static readonly Rule[] Rules =
    {
        // public holidays of both countries
        Fixed(1, 1, HolidayKind.Public, "Πρωτοχρονιά"),
        Fixed(1, 6, HolidayKind.Public, "Θεοφάνεια"),
        Movable(-48, HolidayKind.Public, "Καθαρά Δευτέρα"),
        Fixed(3, 25, HolidayKind.National, "Εθνική εορτή: επέτειος της Επανάστασης του 1821 και Ευαγγελισμός της Θεοτόκου", note: "Είναι και αργία."),
        Movable(-2, HolidayKind.Public, "Μεγάλη Παρασκευή"),
        Movable(0, HolidayKind.Public, "Κυριακή του Πάσχα"),
        Movable(1, HolidayKind.Public, "Δευτέρα του Πάσχα"),
        Fixed(5, 1, HolidayKind.Public, "Εργατική Πρωτομαγιά"),
        Movable(50, HolidayKind.Public, "Αγίου Πνεύματος", Gr),
        Movable(50, HolidayKind.Public, "Κατακλυσμός (Δευτέρα του Αγίου Πνεύματος)", Cy),
        Fixed(8, 15, HolidayKind.Public, "Κοίμηση της Θεοτόκου"),
        Fixed(10, 28, HolidayKind.National, "Εθνική εορτή: Ημέρα του «Όχι»", note: "Είναι και αργία."),
        Fixed(12, 25, HolidayKind.Public, "Χριστούγεννα"),
        Fixed(12, 26, HolidayKind.Public, "Σύναξη της Θεοτόκου (δεύτερη ημέρα των Χριστουγέννων)"),

        // Cyprus only
        Movable(-1, HolidayKind.Public, "Μεγάλο Σάββατο", Cy),
        Movable(2, HolidayKind.Public, "Τρίτη του Πάσχα", Cy),
        Fixed(4, 1, HolidayKind.National, "Εθνική επέτειος της 1ης Απριλίου 1955 (έναρξη του αγώνα της ΕΟΚΑ)", Cy, note: "Είναι και αργία."),
        Fixed(10, 1, HolidayKind.National, "Ημέρα Ανεξαρτησίας της Κύπρου", Cy, note: "Είναι και αργία."),
        Fixed(7, 15, HolidayKind.Remembrance, "Ημέρα μνήμης: το πραξικόπημα του 1974", Cy),
        Fixed(7, 20, HolidayKind.Remembrance, "Ημέρα μνήμης: η τουρκική εισβολή του 1974", Both),

        // days of national memory of Greece (not public holidays)
        Fixed(5, 19, HolidayKind.Remembrance, "Ημέρα μνήμης της Γενοκτονίας των Ελλήνων του Πόντου", Gr),
        Fixed(9, 14, HolidayKind.Remembrance, "Ημέρα εθνικής μνήμης της Γενοκτονίας των Ελλήνων της Μικράς Ασίας", Gr),
        Fixed(11, 17, HolidayKind.Remembrance, "Επέτειος της εξέγερσης του Πολυτεχνείου", Gr, note: "Αργία για τα σχολεία και τα πανεπιστήμια."),

        // local holidays: the feast of the patron saint of the city
        Fixed(1, 17, HolidayKind.Local, "Άγιος Γεώργιος ο Νεομάρτυρας ο εν Ιωαννίνοις", Gr, "Ιωάννινα", "πολιούχος ο Άγιος Γεώργιος ο Νεομάρτυρας, γνωστός και ως Φουστανελάς"),
        Fixed(2, 2, HolidayKind.Local, "Υπαπαντή του Κυρίου", Gr, "Καλαμάτα", "πολιούχος η Υπαπαντή"),
        Fixed(6, 29, HolidayKind.Local, "Άγιοι Απόστολοι Πέτρος και Παύλος", Gr, "Γιαννιτσά", "πολιούχοι οι Άγιοι Απόστολοι Πέτρος και Παύλος"),
        Fixed(11, 4, HolidayKind.Local, "Άγιοι Νίκανδρος και Ιωαννίκιος", Gr, "Αριδαία", "πολιούχοι οι Άγιοι Νίκανδρος και Ιωαννίκιος"),
        Fixed(12, 5, HolidayKind.Local, "Άγιος Σάββας", Gr, "Κάλυμνος", "πολιούχος ο Άγιος Σάββας"),
        Fixed(12, 6, HolidayKind.Local, "Άγιος Νικόλαος", Gr, "Βόλος", "πολιούχος ο Άγιος Νικόλαος ο Θαυματουργός"),
        Fixed(12, 15, HolidayKind.Local, "Άγιος Σεραφείμ", Gr, "Καρδίτσα", "πολιούχος ο Άγιος Σεραφείμ· τα σχολεία κλείνουν"),
        Fixed(12, 17, HolidayKind.Local, "Άγιος Διονύσιος", Gr, "Ζάκυνθος", "δεύτερη γιορτή του πολιούχου· τα σχολεία κλείνουν"),
        Fixed(5, 14, HolidayKind.Local, "Άγιος Ισίδωρος", Gr, "Χίος", "πολιούχος ο Άγιος Ισίδωρος"),
        Fixed(5, 15, HolidayKind.Local, "Άγιος Αχίλλιος", Gr, "Λάρισα", "πολιούχος ο Άγιος Αχίλλιος"),
        Fixed(8, 16, HolidayKind.Local, "Άγιος Γεράσιμος", Gr, "Κεφαλονιά", "προστάτης ο Άγιος Γεράσιμος"),
        Fixed(8, 24, HolidayKind.Local, "Άγιος Διονύσιος", Gr, "Ζάκυνθος", "πολιούχος ο Άγιος Διονύσιος"),
        Fixed(10, 3, HolidayKind.Local, "Άγιος Διονύσιος ο Αρεοπαγίτης", Gr, "Αθήνα", "πολιούχος ο Άγιος Διονύσιος ο Αρεοπαγίτης"),
        Fixed(10, 26, HolidayKind.Local, "Άγιος Δημήτριος", Gr, "Θεσσαλονίκη", "πολιούχος ο Άγιος Δημήτριος· η ημέρα θυμίζει και την απελευθέρωση της πόλης το 1912"),
        Fixed(11, 11, HolidayKind.Local, "Άγιος Μηνάς", Gr, "Ηράκλειο", "πολιούχος ο Άγιος Μηνάς"),
        Fixed(11, 21, HolidayKind.Local, "Εισόδια της Θεοτόκου", Gr, "Χανιά", "πολιούχος η Θεοτόκος των Εισοδίων"),
        Fixed(11, 30, HolidayKind.Local, "Άγιος Ανδρέας", Gr, "Πάτρα", "πολιούχος ο Άγιος Ανδρέας"),
        Fixed(12, 12, HolidayKind.Local, "Άγιος Σπυρίδων", Gr, "Κέρκυρα", "πολιούχος ο Άγιος Σπυρίδων"),
        Fixed(10, 17, HolidayKind.Local, "Άγιος Λάζαρος", Cy, "Λάρνακα", "πολιούχος ο Άγιος Λάζαρος")
    };

    /// <summary>Every holiday of a year for the calendar of a country, in date order.</summary>
    public static IReadOnlyList<Holiday> ForYear(int year, string region)
    {
        var list = new List<Holiday>();
        DateOnly? easter = year is >= 1900 and <= 2099 ? Paschalion.OrthodoxEaster(year) : null;
        foreach (var rule in Rules)
        {
            if (!rule.Regions.Contains(region)) continue;
            DateOnly date;
            if (rule.EasterOffset is { } offset)
            {
                if (easter is null) continue;
                date = easter.Value.AddDays(offset);
            }
            else date = new DateOnly(year, rule.Month!.Value, rule.Day!.Value);
            list.Add(new Holiday(date, rule.Kind, rule.Name, rule.Place, rule.Note));
        }
        return list.OrderBy(h => h.Date).ThenBy(h => h.Kind).ToList();
    }

    public static IReadOnlyList<Holiday> On(DateOnly date, string region) => ForYear(date.Year, region).Where(h => h.Date == date).ToList();

    /// <summary>True for a public holiday or a national feast day (a day off), not for a day of memory or a local holiday.</summary>
    public static bool IsDayOff(DateOnly date, string region) => On(date, region).Any(h => h.Kind is HolidayKind.Public or HolidayKind.National);

    /// <summary>A short line for the text of a day: "Αργία: ...", "Εθνική εορτή: ...", "Τοπική αργία (Θεσσαλονίκη): ...".</summary>
    public static string Line(Holiday h) => h.Kind switch
    {
        HolidayKind.National => h.Name,
        HolidayKind.Public => Loc.T("holiday.public") + ": " + h.Name,
        HolidayKind.Remembrance => h.Name,
        _ => $"{Loc.T("holiday.local")} ({h.Place}): {h.Name}"
    };
}
