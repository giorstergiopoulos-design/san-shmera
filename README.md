# Χρονικολόγιο — Timeline Calendar

<p align="center">
  <strong>Ποιος γιορτάζει σήμερα και τι έγινε «σαν σήμερα» — για την Ελλάδα και την Κύπρο.</strong><br/>
  <strong>Who has a name day today and what happened «on this day» — for Greece and Cyprus.</strong>
</p>

<p align="center">
  <img alt="Platform" src="https://img.shields.io/badge/platform-Windows%2011-0078D6?logo=windows&logoColor=white" />
  <img alt=".NET" src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white" />
  <img alt="Version" src="https://img.shields.io/badge/version-0.9.0-00B4D8" />
  <img alt="License" src="https://img.shields.io/badge/license-MIT-green" />
</p>

*English below / Αγγλικά παρακάτω.*

## Ελληνικά

Το **Χρονικολόγιο** είναι ημερολόγιο εορτών και ιστορίας για Windows: δείχνει τον άγιο της ημέρας, ποιοι γιορτάζουν, αργίες και νηστείες, και ιστορικά γεγονότα, γεννήσεις και θανάτους «σαν σήμερα». Λειτουργεί ως **αυτόνομη εφαρμογή** και ως **plugin του Opsis** (ροή «Σαν σήμερα» στο Opsis News Crawler).

| Λειτουργία | Τι κάνει |
|---|---|
| 📅 Ημέρα | Ο άγιος της ημέρας με εικόνα, σύντομο βίο και τα ονόματα που γιορτάζουν· κάρτες γεγονότων, γεννήσεων και θανάτων, με λωρίδα χρόνου από την αρχαιότητα ως σήμερα |
| 🇬🇷 🇨🇾 Χώρες | Ελλάδα και Κύπρος με δικό τους εορτολόγιο και γεγονότα· δεύτερη ημερομηνία του παλαιού ημερολογίου |
| 🖼 Φωτογραφίες | Πορτρέτα και φωτογραφίες γεγονότων μόνο από το Wikimedia Commons με ελεύθερη άδεια, με δημιουργό, άδεια και σύνδεσμο |
| 🔎 Αναζήτηση | Ctrl+K: όνομα, άγιος ή γεγονός |
| 🎨 Εμφάνιση | Θέματα φωτεινό/σκούρο, τέσσερις διατάξεις (Ημέρα, Κάρτες, Ροή, Καρτέλες), ελληνικό και αγγλικό περιβάλλον |
| 🧩 Opsis | Το plugin δίνει στον Opsis News Crawler τη ροή «Σαν σήμερα» |

### Δεδομένα

- Άγιοι και εορτές: ονομαστικός συναξαριστής από το synaxarion.gr (3.450 μνήμες) και δικό μας εορτολόγιο· οι βίοι γράφονται με δικά μας λόγια.
- Γεννήσεις, θάνατοι και γεγονότα: Βικιπαίδεια (el.wikipedia.org), ξαναγραμμένα με δικά μας λόγια, και greekrewind.gr ως βοήθημα για ελληνικά γεγονότα.
- Κάθε εγγραφή έχει κατάσταση επαλήθευσης. Το «Verified» μπαίνει μόνο όταν έχει ανοιχτεί επίσημη πηγή· αλλιώς «Reviewed».
- Για την αρχαιότητα και τον Μεσαίωνα εμφανίζονται μόνο γεγονότα με τεκμηριωμένη ημερομηνία.
- Τα κείμενα δεν αντιγράφονται από άλλες ιστοσελίδες. Η εφαρμογή δεν είναι επίσημη εκκλησιαστική ή ιστορική πηγή.

### Λήψη και εγκατάσταση

Από τα **Releases** κατέβασε το `ChronikologioSetup.exe` και έλεγξε το SHA-256 από το αρχείο `.sha256`. Ο εγκαταστάτης ανιχνεύει αν υπάρχει Opsis και εγκαθιστά και το plugin. Οι εκτελέσιμοι δεν έχουν ψηφιακή υπογραφή, οπότε το SmartScreen ίσως προειδοποιήσει.

### Build

Απαιτούνται .NET 10 SDK και Inno Setup 6 ή 7.

```bat
run.bat                          :: εκτέλεση από τον κώδικα
test-all.bat                     :: όλα τα τεστ
build-installer.bat              :: τεστ + publish + εγκαταστάτης -> Output\ChronikologioSetup.exe
build-installer.bat skip-tests   :: χωρίς τεστ
```

Μετά την προσθήκη φωτογραφιών τρέξε πρώτα `dotnet run --project tools\ImageConvert -- data` (μετατροπή σε WebP), για να μείνει ο εγκαταστάτης μικρός.

### Δομή

| Φάκελος | Περιεχόμενο |
|---|---|
| `src/Chronikologio.Core` | Μοντέλο, φόρτωση δεδομένων, εορτολόγιο, ροή Opsis |
| `src/Chronikologio.UI` | Οθόνες WinForms (Opsis.UI) |
| `src/Chronikologio.Windows` | Η αυτόνομη εφαρμογή |
| `src/Chronikologio.OpsisPlugin` | Το plugin του Opsis |
| `data` | Τα δεδομένα (TSV/JSON) και οι εικόνες |
| `tools` | Εργαλεία ανάγνωσης και επαλήθευσης (πάντα με ευγενικό ρυθμό και robots.txt) |
| `installer` | Εγκαταστάτης Inno Setup |
| `docs` | Σχεδιασμός και ενσωμάτωση με το Opsis |

Δες και το [CHANGELOG.md](CHANGELOG.md) και το [SECURITY.md](SECURITY.md).

## English

**Chronikologio — Timeline Calendar** is a Windows calendar of name days and history for Greece and Cyprus: the saint of the day, who celebrates, holidays and fasts, and historical events, births and deaths «on this day». It runs as a **stand-alone app** and as an **Opsis plugin** (the «On this day» feed of the Opsis News Crawler).

- **Data:** the saints' commemorations come from synaxarion.gr (3,450 entries) and our own calendar; births, deaths and events come from the Greek Wikipedia and are rewritten in our own words. Every entry shows its verification state («Verified» only after an official source was opened). For antiquity and the Middle Ages only events with a documented date are listed.
- **Images:** only free licences from Wikimedia Commons (public domain, CC0, CC BY), with author, licence and source link.
- **Install:** download `ChronikologioSetup.exe` from Releases and check the SHA-256 in the `.sha256` file. The installer detects Opsis and installs the plugin. Binaries are not code-signed.
- **Build:** .NET 10 SDK and Inno Setup 6/7; see the commands above.
- **Licence:** MIT, see [LICENSE](LICENSE).
