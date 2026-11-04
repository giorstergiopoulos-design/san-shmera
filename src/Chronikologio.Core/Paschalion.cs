namespace Chronikologio.Core;

/// <summary>
/// The Orthodox Easter (Pascha) and the feasts that move with it. Easter is computed with the Julian algorithm of Meeus
/// and converted to the Gregorian calendar (+13 days, valid for 1900-2099), which is the date the Church of Greece uses.
/// </summary>
public static class Paschalion
{
    public static DateOnly OrthodoxEaster(int year)
    {
        if (year is < 1900 or > 2099) throw new ArgumentOutOfRangeException(nameof(year), "Supported years are 1900-2099.");
        var a = year % 4;
        var b = year % 7;
        var c = year % 19;
        var d = (19 * c + 15) % 30;
        var e = (2 * a + 4 * b - d + 34) % 7;
        var month = (d + e + 114) / 31;
        var day = (d + e + 114) % 31 + 1;
        return new DateOnly(year, month, day).AddDays(13);
    }

    private static readonly SourceRef[] Sources =
    {
        new("model-recall", "Σύνταξη από γνώση του μοντέλου. Προς επαλήθευση από το Οικουμενικό Πατριαρχείο και την Εκκλησία της Ελλάδος· η ημερομηνία του Πάσχα υπολογίζεται με τον αλγόριθμο του Μέεους για το Ιουλιανό ημερολόγιο.")
    };

    private static NameDay Name(string name, string[] variants, string origin, string meaning) =>
        new(name, variants, new Etymology(origin, meaning));

    private static Commemoration Feast(string title, string summary, bool major = false, params NameDay[] names) =>
        new(title, summary, null, names, Verification.Unverified, Sources, major);

    /// <summary>The movable feasts of a year, each on its date.</summary>
    public static IReadOnlyList<(DateOnly Date, Commemoration Feast)> Movable(int year, string region = "gr")
    {
        var easter = OrthodoxEaster(year);
        var list = new List<(DateOnly, Commemoration)>
        {
            (easter.AddDays(-48), Feast("Καθαρά Δευτέρα",
                "Αρχίζει η Μεγάλη Τεσσαρακοστή, η περίοδος νηστείας και προετοιμασίας για το Πάσχα. Η ημέρα είναι αργία στην Ελλάδα και συνηθίζεται η έξοδος στην ύπαιθρο και το πέταγμα χαρταετού.")),
            (easter.AddDays(-7), Feast("Κυριακή των Βαΐων",
                "Η Εκκλησία θυμάται την είσοδο του Ιησού στην Ιερουσαλήμ, όπου το πλήθος τον υποδέχτηκε με βάγια, δηλαδή κλαδιά φοίνικα. Την ημέρα αυτή αρχίζει η Μεγάλη Εβδομάδα.", true,
                Name("Βάιος", new[] { "Βάια", "Βαΐα", "Βαγιώ" }, "ελληνική", "Από το «βάιον», το κλαδί του φοίνικα."))),
            (easter.AddDays(-2), Feast("Μεγάλη Παρασκευή",
                "Η Εκκλησία θυμάται τη σταύρωση και την ταφή του Χριστού. Το βράδυ γίνεται η περιφορά του Επιταφίου. Η ημέρα είναι αργία και αυστηρή νηστεία.", true)),
            (easter.AddDays(-1), Feast("Μεγάλο Σάββατο",
                "Η Εκκλησία θυμάται την ταφή του Χριστού και την κάθοδό του στον Άδη. Το βράδυ, στην Ανάσταση, ακούγεται το «Χριστός Ανέστη» και ανάβουν οι λαμπάδες.")),
            (easter, Feast("Κυριακή του Πάσχα (Ανάσταση του Κυρίου)",
                "Η μεγαλύτερη γιορτή της Ορθόδοξης Εκκλησίας, που θυμίζει την Ανάσταση του Χριστού. Την ημέρα αυτή γιορτάζουν όσοι έχουν ονόματα που συνδέονται με την Ανάσταση και τη λαμπρότητα του Πάσχα.", true,
                Name("Αναστάσιος", new[] { "Τάσος", "Αναστασία", "Τασούλα" }, "ελληνική", "Αυτός που αναστήθηκε, ο ζωντανός (από το «ανάσταση»)."),
                Name("Λάμπρος", new[] { "Λαμπρινή", "Λαμπρούλα", "Λάμπης", "Λαμπρινός" }, "ελληνική", "Αυτός που λάμπει, ο λαμπρός· το Πάσχα λέγεται και Λαμπρή."),
                Name("Πασχάλης", new[] { "Πασχαλία", "Πασχαλινή" }, "ελληνική", "Από το «Πάσχα», τη γιορτή της Ανάστασης."))),
            (easter.AddDays(1), Feast("Δευτέρα της Διακαινησίμου",
                "Η δεύτερη ημέρα του Πάσχα, αργία στην Ελλάδα. Όλη η εβδομάδα μετά το Πάσχα λέγεται Διακαινήσιμος και ο ναός δεν κλείνει τις Ωραίες Πύλες της.")),
            (easter.AddDays(39), Feast("Η Ανάληψη του Κυρίου",
                "Σαράντα ημέρες μετά το Πάσχα, η Εκκλησία γιορτάζει την ανάληψη του Χριστού στον ουρανό, μπροστά στους μαθητές του.", true)),
            (easter.AddDays(49), Feast("Η Πεντηκοστή",
                "Πενήντα ημέρες μετά το Πάσχα, η Εκκλησία θυμάται την κάθοδο του Αγίου Πνεύματος στους αποστόλους και θεωρεί την ημέρα γενέθλια της Εκκλησίας.", true)),
            (easter.AddDays(50), Feast("Του Αγίου Πνεύματος",
                "Η Δευτέρα μετά την Πεντηκοστή είναι αργία στην Ελλάδα και τιμά το Άγιο Πνεύμα.")),
            (easter.AddDays(56), Feast("Κυριακή των Αγίων Πάντων",
                "Η Κυριακή μετά την Πεντηκοστή είναι αφιερωμένη σε όλους τους αγίους, γνωστούς και άγνωστους. Μετά από αυτήν αρχίζει η νηστεία των Αγίων Αποστόλων."))
        };

        if (region == "cy") AddCypriotDifferences(list, year, easter);
        return list;
    }

    /// <summary>
    /// What the Church and the State of Cyprus add or call differently: the Monday of the Holy Spirit is the Kataklysmos, Easter Tuesday is also a holiday,
    /// and on the first Sunday of October the Church of Cyprus keeps the synaxis of all the saints of the island.
    /// </summary>
    private static void AddCypriotDifferences(List<(DateOnly, Commemoration)> list, int year, DateOnly easter)
    {
        var holySpiritMonday = easter.AddDays(50);
        var i = list.FindIndex(m => m.Item1 == holySpiritMonday);
        if (i >= 0)
            list[i] = (holySpiritMonday, Feast("Κατακλυσμός· Δευτέρα του Αγίου Πνεύματος",
                "Στην Κύπρο η Δευτέρα του Αγίου Πνεύματος λέγεται Κατακλυσμός και είναι αργία. Η γιορτή συνδέεται με τον κατακλυσμό του Νώε και κυρίως με το νερό και τη θάλασσα. Οι μεγαλύτερες εκδηλώσεις γίνονται στη Λάρνακα και σε άλλες παραθαλάσσιες πόλεις."));

        list.Add((easter.AddDays(2), Feast("Τρίτη της Διακαινησίμου",
            "Στην Κύπρο και η τρίτη ημέρα του Πάσχα είναι αργία.")));

        var october1 = new DateOnly(year, 10, 1);
        var firstSunday = october1.AddDays((7 - (int)october1.DayOfWeek) % 7);
        list.Add((firstSunday, Feast("Σύναξη πάντων των εν Κύπρω διαλαμψάντων Αγίων",
            "Την πρώτη Κυριακή του Οκτωβρίου η Εκκλησία της Κύπρου τιμά όλους τους αγίους που έζησαν και διακρίθηκαν στο νησί, γνωστούς και άγνωστους.", true)));
    }
}
