using System.Globalization;

namespace Chronikologio.Core;

/// <summary>A country the app has a calendar for: the data region and the first official language of the country, which the interface then speaks.</summary>
public sealed record Country(string Region, string Language);

public static class Countries
{
    /// <summary>
    /// The countries in the order of the flag menu. Cyprus has two official languages; the first, Greek, is the interface language.
    /// A later version adds a row here (and a table in <see cref="Loc"/>) for each new country.
    /// </summary>
    public static readonly IReadOnlyList<Country> All = new[]
    {
        new Country("gr", "el"),
        new Country("cy", "el")
    };

    /// <summary>True when the event belongs to the history of the chosen country (the first frame of the day); everything else is "abroad".</summary>
    public static bool IsDomestic(string region, HistoricalEvent e) => region == "cy" ? e.IsCypriot : e.IsGreek;

    public static string LanguageOf(string region) => All.FirstOrDefault(c => c.Region == region)?.Language ?? "el";

    /// <summary>The interface language: the one the user chose ("el", "en"), or, for "auto" (the default), the language of the country.</summary>
    public static string Resolve(string? setting, string region) => setting is "el" or "en" ? setting : LanguageOf(region);
}

/// <summary>
/// The texts of the interface (not of the database) in the language of the chosen country. Keys missing from a language fall back to Greek,
/// so a new language can be added table by table.
/// </summary>
public static class Loc
{
    private static readonly Dictionary<string, Dictionary<string, string>> Tables = new()
    {
        ["el"] = new()
        {
            ["app.title"] = "Χρονικολόγιο — Timeline Calendar",
            ["search.placeholder"] = "Αναζήτηση (όνομα, άγιος, γεγονός)…",
            ["search.none"] = "Δεν βρέθηκε στη βάση.",
            ["panel.more"] = "Πλήρες κείμενο και πηγές…",
            ["dialog.title"] = "Χρονικολόγιο",
            ["flag.tip"] = "Χώρα και εορτολόγιο",
            ["region.gr"] = "Ελλάδα — Εκκλησία της Ελλάδος",
            ["region.cy"] = "Κύπρος — Εκκλησία της Κύπρου",
            ["menu.file"] = "Αρχείο",
            ["menu.exit"] = "Έξοδος",
            ["menu.view"] = "Προβολή",
            ["menu.today"] = "Σήμερα",
            ["menu.theme"] = "Θέμα",
            ["menu.calendar"] = "Εορτολόγιο",
            ["menu.language"] = "Γλώσσα",
            ["menu.settings"] = "Ρυθμίσεις",
            ["settings.title"] = "Ρυθμίσεις",
            ["settings.group.look"] = "Εμφάνιση",
            ["settings.group.calendar"] = "Χώρα και γλώσσα",
            ["settings.theme"] = "Θέμα",
            ["settings.layout"] = "Διάταξη",
            ["settings.menubar"] = "Εμφάνιση της γραμμής μενού (Ctrl+M)",
            ["settings.region"] = "Χώρα και εορτολόγιο",
            ["settings.language"] = "Γλώσσα εφαρμογής",
            ["settings.reset"] = "Επαναφορά εμφάνισης",
            ["settings.close"] = "Κλείσιμο",
            ["settings.note"] = "Οι αλλαγές εφαρμόζονται αμέσως και αποθηκεύονται.",
            ["language.auto"] = "Αυτόματη (η γλώσσα της χώρας)",
            ["language.el"] = "Ελληνικά",
            ["language.en"] = "English",
            ["menu.hidemenu"] = "Απόκρυψη μενού",
            ["menu.help"] = "Βοήθεια",
            ["menu.shortcuts"] = "Συντομεύσεις",
            ["old.calendar"] = "παλαιό ημερολόγιο",
            ["event.nophoto"] = "Δεν υπάρχει ακόμη φωτογραφία για το γεγονός",
            ["event.source"] = "Πηγή",
            ["event.more"] = "περισσότερα",
            ["event.none.gr"] = "Δεν υπάρχει ακόμη ελληνικό γεγονός για αυτή την ημέρα.",
            ["event.none.world"] = "Δεν υπάρχει ακόμη γεγονός από το εξωτερικό για αυτή την ημέρα.",
            ["frame.gr"] = "Σαν σήμερα στην Ελλάδα",
            ["frame.gr.cy"] = "Σαν σήμερα στην Κύπρο",
            ["event.none.gr.cy"] = "Δεν υπάρχει ακόμη κυπριακό γεγονός για αυτή την ημέρα.",
            ["short.history.gr.cy"] = "Σαν σήμερα στην κυπριακή ιστορία",
            ["frame.world"] = "Σαν σήμερα στο εξωτερικό",
            ["holiday.public"] = "Αργία",
            ["holiday.local"] = "Τοπική αργία",
            ["group.fasts"] = "Νηστείες",
            ["group.movable"] = "Κινητές εορτές",
            ["group.national"] = "Εθνικές εορτές και αργίες",
            ["group.memory"] = "Ημέρες μνήμης",
            ["group.local"] = "Τοπικές αργίες των πολιούχων αγίων",
            ["fast.strict"] = "αυστηρή νηστεία",
            ["fast.lent"] = "νηστεία χωρίς κρέας, ψάρι, γαλακτοκομικά και αυγά· λάδι τις Κυριακές και τα Σάββατα",
            ["fast.fish"] = "νηστεία χωρίς κρέας και γαλακτοκομικά· ψάρι σε ορισμένες ημέρες",
            ["fast.free"] = "δεν υπάρχει νηστεία",
            ["fast.dairy"] = "επιτρέπονται γαλακτοκομικά και αυγά, όχι κρέας",
            ["col.kind"] = "Είδος",
            ["menu.layout"] = "Διάταξη",
            ["layout.cards"] = "Κάρτες «Σήμερα»",
            ["layout.feed"] = "Ροή ημέρας",
            ["layout.tabs"] = "Καρτέλες",
            ["btn.today"] = "Σήμερα",
            ["card.saint"] = "Άγιοι και ονόματα",
            ["tab.today"] = "Σήμερα",
            ["tab.calendar"] = "Ημερολόγιο",
            ["tab.names"] = "Ονόματα",
            ["tab.timeline"] = "Χρονογραμμή",
            ["tab.feasts"] = "Νηστείες και εορτές",
            ["tab.easter"] = "Πάσχα",
            ["search.names"] = "Αναζήτηση ονόματος…",
            ["col.name"] = "Όνομα",
            ["col.day"] = "Ημέρα",
            ["col.date"] = "Ημερομηνία",
            ["col.meaning"] = "Σημασία",
            ["col.feast"] = "Εορτή",
            ["col.event"] = "Σαν σήμερα",
            ["menu.guide"] = "Οδηγίες χρήσης",
            ["menu.changelog"] = "Ιστορικό εκδόσεων",
            ["guide.title"] = "Οδηγίες χρήσης",
            ["menu.license"] = "Άδεια χρήσης",
            ["menu.about"] = "Σχετικά",
            ["help.title"] = "Σχετικά",
            ["help.tab.guide"] = "Οδηγίες χρήσης",
            ["help.tab.changelog"] = "Ιστορικό εκδόσεων",
            ["help.tab.license"] = "Άδεια χρήσης",
            ["help.tab.sources"] = "Πηγές",
            ["help.license.summary"] = "Ελεύθερο λογισμικό",
            ["help.license.disclaimer"] = "Αποποίηση ευθύνης",
            ["help.license.content"] = "Περιεχόμενο και εικόνες",
            ["help.license.terms"] = "Όροι της άδειας MIT",
            ["help.sources.data"] = "Κατάσταση των δεδομένων",
            ["help.sources.data.text"] = "Κάθε εγγραφή έχει κατάσταση: «δεν έχει επαληθευτεί», «διασταυρώθηκε» (η ημερομηνία συμφωνεί με ανεξάρτητη πηγή) ή «επαληθευμένη» (με επίσημη πηγή). Οι βίοι των αγίων και οι ετυμολογίες είναι γραμμένα από την αρχή και μπορεί να έχουν λάθη· διόρθωσέ τα με τον Συναξαριστή του Οικουμενικού Πατριαρχείου.",
            ["changelog.title"] = "Ιστορικό εκδόσεων",
            ["guide.text"] =
                "ΤΙ ΕΙΝΑΙ\n" +
                "Το Χρονικολόγιο δείχνει για κάθε ημέρα του έτους ποιοι άγιοι τιμώνται, ποια ονόματα γιορτάζουν και τι σημαίνουν, τι έγινε σαν σήμερα στην ελληνική και στην παγκόσμια ιστορία, και ποιοι γεννήθηκαν.\n\n" +
                "ΧΡΗΣΗ\n" +
                "• Πάτησε μια ημέρα στο ημερολόγιο. Κάτω από αυτό εμφανίζεται σύντομο κείμενο και, αν υπάρχει, μια εικόνα.\n" +
                "• Το κουμπί «Πλήρες κείμενο και πηγές» (ή διπλό κλικ στο σύντομο κείμενο) ανοίγει το πλήρες κείμενο, με τις εικόνες δίπλα του.\n" +
                "• Η σημαία πάνω αριστερά αλλάζει τη χώρα: το εορτολόγιο αλλάζει μαζί της, και η γλώσσα της εφαρμογής επίσης, εκτός αν διαλέξεις γλώσσα από το μενού Προβολή › Γλώσσα. Τώρα υπάρχουν η Ελλάδα και η Κύπρος.\n" +
                "• Η αναζήτηση ονόματος βρίσκει πότε γιορτάζει ένα όνομα, με ή χωρίς τόνους.\n" +
                "• Οι μεγάλες εορτές φαίνονται με έντονο αριθμό και μια μικρή γραμμή κάτω από την ημέρα.\n\n" +
                "ΕΜΦΑΝΙΣΗ\n" +
                "Το μενού εμφανίζεται και κρύβεται με Ctrl+M. Το Ctrl+T αλλάζει το θέμα. Μπορείς να διαλέξεις θέμα και από το μενού Προβολή.\n" +
                "Η διάταξη αλλάζει από το μενού Προβολή › Διάταξη ή με Ctrl+L. Υπάρχουν τρεις: κάρτες «Σήμερα» (τέσσερις κάρτες και μικρό ημερολόγιο), ροή ημέρας (ταινία ημερών και ανάγνωση σε μία στήλη με την εικόνα δίπλα) και καρτέλες (Σήμερα, Ημερολόγιο, Ονόματα, Χρονογραμμή του μήνα, Νηστείες και εορτές του έτους). Η επιλογή σου αποθηκεύεται.\n\n" +
                "ΠΡΟΣΟΧΗ ΣΤΑ ΔΕΔΟΜΕΝΑ\n" +
                "Τα κείμενα είναι γραμμένα από την αρχή και δεν έχουν ακόμη επαληθευτεί με τις επίσημες πηγές. Στο πλήρες κείμενο φαίνεται η κατάσταση κάθε εγγραφής. Οι εγκυκλοπαιδικοί σύνδεσμοι είναι μόνο για περισσότερες πληροφορίες.\n\n" +
                "ΠΗΓΕΣ ΚΑΙ ΕΥΧΑΡΙΣΤΙΕΣ\n" +
                "• Ο Συναξαριστής του saint.gr (https://www.saint.gr), για τη λίστα των αγίων κάθε ημέρας και τους συνδέσμους προς τους βίους τους. Τα κείμενα της εφαρμογής είναι γραμμένα με δικά μας λόγια.\n" +
                "• Το ανοιχτό εορτολόγιο Orthocal (https://orthocal.info), για τη διασταύρωση των ημερομηνιών.\n" +
                "• Οι στήλες «Σαν σήμερα» ειδησεογραφικών sites (Newsbeast, Ethnos, CNN Greece, Parapolitika), για τη διασταύρωση των ημερομηνιών των γεγονότων. Τα κείμενά τους δεν αντιγράφονται· τα γεγονότα γράφονται με δικά μας λόγια.\n" +
                "• Το Wikimedia Commons, για τις εικόνες με ελεύθερη άδεια, και η Wikipedia μόνο για περισσότερες πληροφορίες.\n" +
                "• Οι επίσημες σημαίες Ελλάδας και Κύπρου, από το Wikimedia Commons.\n\n" +
                "ΜΕ ΤΟ OPSIS\n" +
                "Ως πρόσθετο του Opsis, το ημερολόγιο αντικαθιστά το ενσωματωμένο και η ροή «Σαν σήμερα» μπορεί να εμφανίζεται στις μπάρες του News Crawler.",
            ["about.format"] = "Χρονικολόγιο — έκδοση {0}",
            ["help.text"] = "Ctrl+M: εμφάνιση ή απόκρυψη του μενού\nCtrl+T: αλλαγή θέματος\nCtrl+L: αλλαγή διάταξης\nCtrl+Home: σήμερα\nF1: οδηγίες χρήσης\n\n" +
                            "Η σημαία πάνω αριστερά αλλάζει τη χώρα, δηλαδή το εορτολόγιο και, αν δεν έχεις διαλέξει γλώσσα, τη γλώσσα της εφαρμογής.\n" +
                            "Διπλό κλικ στο σύντομο κείμενο ή πάτημα του κουμπιού κάτω από αυτό ανοίγει το πλήρες κείμενο με τις πηγές.",
            ["short.noentries"] = "Δεν υπάρχουν ακόμη εγγραφές για αυτή την ημέρα.",
            ["short.noothers"] = "Δεν υπάρχουν ακόμη άλλες εγγραφές για αυτή την ημέρα.",
            ["short.names"] = "Γιορτάζουν: ",
            ["short.births"] = "Γεννήθηκαν: ",
            ["short.history.gr"] = "Σαν σήμερα στην ελληνική ιστορία",
            ["short.history.world"] = "Σαν σήμερα στην παγκόσμια ιστορία",
            ["details.empty"] = "\nΗ βάση δεν έχει ακόμη εγγραφές για αυτή την ημέρα.",
            ["details.births"] = "\nΓεννήθηκαν σαν σήμερα",
            ["details.deaths"] = "\nΠέθαναν σαν σήμερα",
            ["card.people"] = "Γεννήσεις και θάνατοι",
            ["feed.death"] = "Πέθαναν σαν σήμερα, {0}: {1}",
            ["details.sources"] = "\nΠηγές",
            ["details.unverified"] = " (δεν έχουν ακόμη επαληθευτεί)",
            ["details.reviewed"] = " (η ημερομηνία διασταυρώθηκε με ανεξάρτητη πηγή)",
            ["details.verified"] = " (επαληθευμένα)",
            ["details.synaxarion"] = "Συναξαριστής saint.gr (ο βίος του αγίου): ",
            ["details.official"] = "επίσημη πηγή: ",
            ["details.pointer"] = "για περισσότερα (ανοιχτή εγκυκλοπαίδεια, όχι επίσημη πηγή): ",
            ["feed.names"] = "Γιορτάζουν σήμερα, {0}: {1}",
            ["feed.saint"] = "{0}: {1}",
            ["feed.event"] = "Σαν σήμερα, {0}: {1}",
            ["feed.event.year"] = "Σαν σήμερα, {0} {1}: {2}",
            ["feed.birth"] = "Γεννήθηκαν σαν σήμερα, {0}: {1}"
        }
    };

    static Loc() => Tables["en"] = LocalizationEn.Table();

    /// <summary>The languages the interface has a table for.</summary>
    public static IReadOnlyList<string> Languages => Tables.Keys.ToList();

    /// <summary>The keys of one language (for the tests that keep the tables in step).</summary>
    public static IReadOnlyCollection<string> KeysOf(string language) => Tables[language].Keys;

    /// <summary>The text of a key in a given language (Greek when that language lacks it), without changing the language of the app.</summary>
    public static string TIn(string language, string key) =>
        Tables.TryGetValue(language, out var table) && table.TryGetValue(key, out var s) ? s : Tables["el"].TryGetValue(key, out s) ? s : key;

    private static string _language = "el";

    /// <summary>Raised after the language changed, so open windows can redraw their texts.</summary>
    public static event Action? Changed;

    public static string Language => _language;

    public static CultureInfo Culture => CultureInfo.GetCultureInfo(_language switch { "el" => "el-GR", "en" => "en-GB", _ => _language });

    /// <summary>Switches the language; false (and nothing changes) when the app has no table for it.</summary>
    public static bool Set(string language)
    {
        if (!Tables.ContainsKey(language)) return false;
        if (language == _language) return true;
        _language = language;
        Changed?.Invoke();
        return true;
    }

    /// <summary>A text that has a version for a country (key + "." + region, e.g. "frame.gr.cy"); the base key when it has none.</summary>
    public static string TR(string key, string region) =>
        Tables[_language].TryGetValue(key + "." + region, out var s) || Tables["el"].TryGetValue(key + "." + region, out s) ? s : T(key);

    public static string T(string key) =>
        Tables[_language].TryGetValue(key, out var s) || Tables["el"].TryGetValue(key, out s) ? s : key;
}
