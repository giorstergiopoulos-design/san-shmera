using System.Text.Json;

namespace Chronikologio.Core;

/// <summary>Loads the month files under a data folder (data/&lt;tradition&gt;/&lt;MM&gt;.json) and answers per-date and per-name queries.</summary>
public sealed class DayStore
{
    internal static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip };

    private readonly Dictionary<(int Month, int Day), (List<Commemoration> C, List<HistoricalEvent> E)> _days = new();

    public IReadOnlyList<string> Errors { get; private set; } = Array.Empty<string>();

    public string DataRoot { get; private set; } = "";

    /// <summary>"gr" = the calendar of the Church of Greece, "cy" = the calendar of the Church of Cyprus (the common feasts plus the Cypriot saints, holidays and history).</summary>
    public string Region { get; private set; } = "gr";

    public static readonly IReadOnlyList<string> Regions = new[] { "gr", "cy" };

    private static readonly Dictionary<string, HashSet<string>> TraditionsOf = new()
    {
        ["gr"] = new(StringComparer.Ordinal) { "orthodox-el", "history" },
        ["cy"] = new(StringComparer.Ordinal) { "orthodox-el", "orthodox-cy", "history", "history-cy" }
    };

    /// <summary>Lower number = listed first. Cyprus: Cypriot, then Greek, then the world. Greece: Greek world (Cyprus included), then the world.</summary>
    private int Rank(string region, bool isGreekWorld) =>
        Region == "cy" ? (region == "Κύπρος" ? 0 : isGreekWorld ? 1 : 2) : (isGreekWorld ? 0 : 1);

    /// <summary>Full path of an image file under data/images, or null when the name tries to leave that folder or the file is missing.</summary>
    public string? ImagePath(ImageRef image)
    {
        var root = Path.GetFullPath(Path.Combine(DataRoot, "images"));
        var full = Path.GetFullPath(Path.Combine(root, image.File));
        return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(full) ? full : null;
    }

    public static DayStore Load(string dataRoot, string region = "gr")
    {
        if (!TraditionsOf.ContainsKey(region)) region = "gr";
        var store = new DayStore { DataRoot = dataRoot, Region = region };
        var errors = new List<string>();
        if (Directory.Exists(dataRoot))
        {
            foreach (var file in Directory.EnumerateFiles(dataRoot, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                try
                {
                    var mf = JsonSerializer.Deserialize<MonthFile>(File.ReadAllText(file), Json);
                    if (mf is null) { errors.Add($"{file}: empty"); continue; }
                    store.Add(mf);
                }
                catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
                {
                    errors.Add($"{file}: {ex.Message}");
                }
            }
        }
        if (Directory.Exists(dataRoot))
            foreach (var file in Directory.EnumerateFiles(dataRoot, "*.tsv", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                // births-cy.tsv belongs to the Cyprus calendar only
                if (Path.GetFileNameWithoutExtension(file).EndsWith("-cy", StringComparison.Ordinal) && region != "cy") continue;
                try
                {
                    var name = Path.GetFileName(file);
                    if (name.Equals(EntryIds.RegistryFile, StringComparison.Ordinal)) continue;   // read by ApplyIds
                    // the saints of the Megas Synaxaristis of the Church of Greece belong to the Greek calendar only
                    if (name.StartsWith("saints-syn", StringComparison.Ordinal) && region != "gr") continue;
                    if (name.StartsWith("lives", StringComparison.Ordinal)) store.AddLives(file, errors);
                    else if (name.StartsWith("saints", StringComparison.Ordinal)) store.AddSaints(file, errors);
                    else if (name.StartsWith("events", StringComparison.Ordinal)) store.AddEvents(file, errors);
                    else if (name.StartsWith("verification", StringComparison.Ordinal)) store.AddChecks(file, errors);
                    else if (name.StartsWith("eventimages", StringComparison.Ordinal)) store.AddEventImages(file, errors);
                    else if (name.StartsWith("saintimages", StringComparison.Ordinal)) store.AddSaintImages(file, errors);
                    else if (name.StartsWith("deaths", StringComparison.Ordinal)) store.AddBirths(file, errors, store._deaths, "deaths");
                    else store.AddBirths(file, errors, store._births, "births");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { errors.Add($"{file}: {ex.Message}"); }
            }
        store.ApplySupplements();
        store.ApplyLives();
        store.RemoveRepeatedEvents();
        store.ApplyIds(dataRoot);
        store.ApplyChecks();
        store.ApplyEventImages();
        store.ApplySaintImages();
        store.Errors = errors;
        return store;
    }

    private readonly List<(int Month, int Day, int Year, SourceRef[] Sources)> _supplements = new();

    /// <summary>Adds the supplementary sources to the events of the same date and year, whichever file those events came from.</summary>
    private void ApplySupplements()
    {
        foreach (var (month, day, year, sources) in _supplements)
        {
            if (!_days.TryGetValue((month, day), out var slot)) continue;
            for (var i = 0; i < slot.E.Count; i++)
            {
                if (slot.E[i].Year != year) continue;
                var extra = sources.Where(s => !slot.E[i].Sources.Any(x => x.Url == s.Url && s.Url is not null));
                slot.E[i] = slot.E[i] with { Sources = slot.E[i].Sources.Concat(extra).ToArray() };
            }
        }
    }

    private void Add(MonthFile mf)
    {
        if (!TraditionsOf[Region].Contains(mf.Tradition)) return;   // another country's calendar
        foreach (var d in mf.Days)
        {
            if (d.SourceSupplements is not null)
                foreach (var s in d.SourceSupplements) _supplements.Add((mf.Month, d.Day, s.Year, s.Sources));
            if (!_days.TryGetValue((mf.Month, d.Day), out var slot))
                _days[(mf.Month, d.Day)] = slot = (new(), new());
            var fallback = mf.DefaultSources ?? Array.Empty<SourceRef>();
            if (d.Commemorations is not null)
                slot.C.AddRange(d.Commemorations.Select(c => c with
                {
                    Sources = c.Sources is { Length: > 0 } ? c.Sources : fallback,
                    Names = (c.Names ?? Array.Empty<NameDay>()).Select(n => n with { Variants = n.Variants ?? Array.Empty<string>() }).ToArray()
                }));
            if (d.Events is not null)
                slot.E.AddRange(d.Events.Select(e => e with { Sources = e.Sources is { Length: > 0 } ? e.Sources : fallback }));
        }
    }

    private static readonly SourceRef[] SaintsSource =
    {
        new("model-recall", "Σύνταξη από γνώση του μοντέλου, από την αρχή. Η ημερομηνία και τα στοιχεία προς επαλήθευση από το Συναξάριο του Οικουμενικού Πατριαρχείου και την Εκκλησία της Ελλάδος.")
    };

    /// <summary>
    /// Reads the saints files (saints*.tsv): one line per commemoration, "MM-DD|title|summary|names";
    /// names are separated by ";" and each is "Name~variant,variant~origin~meaning". A bad line is reported and skipped.
    /// </summary>
    private void AddSaints(string file, List<string> errors)
    {
        var number = 0;
        foreach (var line in File.ReadLines(file))
        {
            number++;
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
            var f = line.Split('|');
            if ((f.Length != 4 && f.Length != 5) || f[0].Length != 5 || f[0][2] != '-'
                || !int.TryParse(f[0][..2], out var month) || !int.TryParse(f[0][3..], out var day)
                || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(2024, month))
            {
                errors.Add($"{file}:{number}: bad saints line");
                continue;
            }
            var names = new List<NameDay>();
            foreach (var part in f[3].Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var n = part.Split('~');
                if (n.Length != 4) { errors.Add($"{file}:{number}: bad name '{part}'"); continue; }
                names.Add(new NameDay(n[0].Trim(),
                    n[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    new Etymology(n[2].Trim(), n[3].Trim())));
            }
            if (!_days.TryGetValue((month, day), out var slot)) _days[(month, day)] = slot = (new(), new());
            // a fifth field is the number of the page of the saint on synaxarion.gr: the memory was found in the Megas Synaxaristis, so the date is cross-checked (Reviewed) and the page is the source
            var fromSynaxarion = f.Length == 5 && f[4].Trim().Length > 0;
            var sources = fromSynaxarion ? new[] { new SourceRef("calendar-crosscheck", "Μέγας Συναξαριστής (synaxarion.gr)", $"https://www.synaxarion.gr/gr/sid/{f[4].Trim()}/sxsaintinfo.aspx") } : SaintsSource;
            slot.C.Add(new Commemoration(f[1].Trim(), f[2].Trim(), null, names.ToArray(), fromSynaxarion ? Verification.Reviewed : Verification.Unverified, sources));
        }
    }

    /// <summary>
    /// The same event can come from two files (an older list and a newer one). Within a day, an event of the same year whose title says the same thing
    /// as an earlier one (the same title, or two long words in common) is dropped; the one with the longer text stays.
    /// </summary>
    private void RemoveRepeatedEvents()
    {
        static HashSet<string> Words(string title) =>
            TextNorm.Fold(title).Split(new[] { ' ', '.', ',', '«', '»', '"', '·', ':', '-', '–' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 5).ToHashSet();

        foreach (var slot in _days.Values)
        {
            var events = slot.E;
            for (var i = events.Count - 1; i >= 0; i--)
                for (var j = 0; j < i; j++)
                {
                    if (events[i].Year != events[j].Year || events[i].Region != events[j].Region) continue;
                    var same = TextNorm.Fold(events[i].Title) == TextNorm.Fold(events[j].Title) || Words(events[i].Title).Intersect(Words(events[j].Title)).Count() >= 2;
                    if (!same) continue;
                    if (events[i].Summary.Length > events[j].Summary.Length) events[j] = events[i];
                    events.RemoveAt(i);
                    break;
                }
        }
    }

    private readonly List<(int Month, int Day, string Title, string Life)> _lives = new();

    /// <summary>
    /// Reads the lives of the saints (lives*.tsv): "MM-DD|title|life", the title being the beginning of the title of the commemoration. The life is written from scratch and
    /// is shown in the details of the commemoration (the summary stays the short text). A bad line is reported and skipped.
    /// </summary>
    private void AddLives(string file, List<string> errors)
    {
        var number = 0;
        foreach (var line in File.ReadLines(file))
        {
            number++;
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
            var f = line.Split('|');
            if (f.Length != 3 || f[0].Length != 5 || f[0][2] != '-' || !int.TryParse(f[0][..2], out var month) || !int.TryParse(f[0][3..], out var day)
                || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(2024, month) || f[2].Trim().Length < 40)
            {
                errors.Add($"{file}:{number}: bad life line");
                continue;
            }
            _lives.Add((month, day, f[1].Trim(), f[2].Trim().Replace(" // ", "\n\n")));
        }
    }

    private void ApplyLives()
    {
        foreach (var (month, day, title, life) in _lives)
        {
            if (!_days.TryGetValue((month, day), out var slot)) continue;
            for (var i = 0; i < slot.C.Count; i++)
                if (TextNorm.StartsWithWord(slot.C[i].Title, title)) slot.C[i] = slot.C[i] with { Details = life };
        }
    }

    private readonly List<(int Month, int Day, int? Year, string Title, ImageRef Image)> _eventImages = new();

    /// <summary>
    /// Reads the photographs of events (eventimages*.tsv): "MM-DD|year|title|file|caption|creator|license|sourceUrl". The file is relative to data/images.
    /// Only images with a licence that allows reuse are listed (the tools check the licence); the creator, licence and source page are shown with the image.
    /// </summary>
    private void AddEventImages(string file, List<string> errors)
    {
        var number = 0;
        foreach (var line in File.ReadLines(file))
        {
            number++;
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
            var f = line.Split('|');
            if (f.Length != 8 || f[0].Length != 5 || f[0][2] != '-'
                || !int.TryParse(f[0][..2], out var month) || !int.TryParse(f[0][3..], out var day)
                || (f[1].Length > 0 && !int.TryParse(f[1], out _))
                || string.IsNullOrWhiteSpace(f[3]) || string.IsNullOrWhiteSpace(f[5]) || string.IsNullOrWhiteSpace(f[6]) || string.IsNullOrWhiteSpace(f[7]))
            {
                errors.Add($"{file}:{number}: bad event image line");
                continue;
            }
            int? year = f[1].Length > 0 ? int.Parse(f[1], System.Globalization.CultureInfo.InvariantCulture) : null;
            _eventImages.Add((month, day, year, f[2].Trim(), new ImageRef(f[3].Trim(), f[4].Trim(), f[5].Trim(), f[6].Trim(), f[7].Trim(), "photo")));
        }
    }

    private readonly List<(int Month, int Day, string Title, ImageRef Image)> _saintImages = new();

    /// <summary>Reads the icons of the saints (saintimages*.tsv): "MM-DD|beginning of the title|file|caption|creator|license|sourceUrl".</summary>
    private void AddSaintImages(string file, List<string> errors)
    {
        var number = 0;
        foreach (var line in File.ReadLines(file))
        {
            number++;
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
            var f = line.Split('|');
            if (f.Length != 7 || f[0].Length != 5 || f[0][2] != '-' || !int.TryParse(f[0][..2], out var month) || !int.TryParse(f[0][3..], out var day)
                || string.IsNullOrWhiteSpace(f[1]) || string.IsNullOrWhiteSpace(f[2]) || string.IsNullOrWhiteSpace(f[4]) || string.IsNullOrWhiteSpace(f[5]) || string.IsNullOrWhiteSpace(f[6]))
            {
                errors.Add($"{file}:{number}: bad saint image line");
                continue;
            }
            _saintImages.Add((month, day, f[1].Trim(), new ImageRef(f[2].Trim(), f[3].Trim(), f[4].Trim(), f[5].Trim(), f[6].Trim(), "icon")));
        }
    }

    private void ApplySaintImages()
    {
        foreach (var (month, day, title, image) in _saintImages)
        {
            if (!_days.TryGetValue((month, day), out var slot)) continue;
            for (var i = 0; i < slot.C.Count; i++)
                if (TextNorm.StartsWithWord(slot.C[i].Title, title) && !(slot.C[i].Images ?? Array.Empty<ImageRef>()).Any(x => x.File == image.File))
                    slot.C[i] = slot.C[i] with { Images = (slot.C[i].Images ?? Array.Empty<ImageRef>()).Append(image).ToArray() };
        }
    }

    private void ApplyEventImages()
    {
        foreach (var (month, day, year, title, image) in _eventImages)
        {
            if (!_days.TryGetValue((month, day), out var slot)) continue;
            // the photograph belongs to the event of that year with that title; when the same event is listed under another wording (two lists were merged),
            // the only event of that year, or the one with the most words in common, takes it
            var sameYear = Enumerable.Range(0, slot.E.Count).Where(i => slot.E[i].Year == year).ToList();
            var target = sameYear.FirstOrDefault(i => slot.E[i].Title == title, -1);
            if (target < 0 && sameYear.Count == 1) target = sameYear[0];
            if (target < 0 && sameYear.Count > 1)
            {
                var wanted = TextNorm.Fold(title).Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 5).ToHashSet();
                target = sameYear.OrderByDescending(i => TextNorm.Fold(slot.E[i].Title).Split(' ', StringSplitOptions.RemoveEmptyEntries).Count(wanted.Contains)).First();
                if (!TextNorm.Fold(slot.E[target].Title).Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(wanted.Contains)) target = -1;
            }
            if (target >= 0 && !(slot.E[target].Images ?? Array.Empty<ImageRef>()).Any(x => x.File == image.File))
                slot.E[target] = slot.E[target] with { Images = (slot.E[target].Images ?? Array.Empty<ImageRef>()).Append(image).ToArray() };
        }
    }

    private readonly List<(string Kind, int Month, int Day, int? Year, string Title, Verification Status, string? Url, string Citation)> _checks = new();

    /// <summary>
    /// Reads the verification files (verification*.tsv): "kind|MM-DD|year|title|status|url|citation", kind = saint or event, year empty for a saint,
    /// status = Reviewed or Verified. Each line raises the status of the record with that date (and year) and title and adds the checking source to it.
    /// A Verified line must carry a URL.
    /// </summary>
    private void AddChecks(string file, List<string> errors)
    {
        var number = 0;
        foreach (var line in File.ReadLines(file))
        {
            number++;
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
            var f = line.Split('|');
            if (f.Length != 7 || f[1].Length != 5 || f[1][2] != '-'
                || !int.TryParse(f[1][..2], out var month) || !int.TryParse(f[1][3..], out var day)
                || (f[0] != "saint" && f[0] != "event" && f[0] != "source")
                || !Enum.TryParse<Verification>(f[4], out var status) || (status == Verification.Unverified && f[0] != "source")
                || (status == Verification.Verified && string.IsNullOrWhiteSpace(f[5])))
            {
                errors.Add($"{file}:{number}: bad verification line");
                continue;
            }
            int? year = f[2].Length > 0 && int.TryParse(f[2], out var y) ? y : null;
            _checks.Add((f[0], month, day, year, f[3].Trim(), status, f[5].Length > 0 ? f[5] : null, f[6].Trim()));
        }
    }

    private void ApplyChecks()
    {
        foreach (var (kind, month, day, year, title, status, url, citation) in _checks)
        {
            if (!_days.TryGetValue((month, day), out var slot)) continue;
            if (kind == "source")
            {
                // a link to the life at the Synaxarion of saint.gr: added after the text; the status of the record is not changed
                var link = new SourceRef("synaxarion", citation, url);
                for (var i = 0; i < slot.C.Count; i++)
                    if (slot.C[i].Title == title && !slot.C[i].Sources.Any(s => s.Url == url))
                        slot.C[i] = slot.C[i] with { Sources = slot.C[i].Sources.Append(link).ToArray() };
                continue;
            }
            var source = new SourceRef("calendar-crosscheck", citation, url);
            if (kind == "saint")
            {
                for (var i = 0; i < slot.C.Count; i++)
                    if (slot.C[i].Title == title && slot.C[i].Status < status)
                        slot.C[i] = slot.C[i] with { Status = status, Sources = slot.C[i].Sources.Append(source).ToArray() };
            }
            else
            {
                for (var i = 0; i < slot.E.Count; i++)
                    if (slot.E[i].Year == year && slot.E[i].Title == title && slot.E[i].Status < status)
                        slot.E[i] = slot.E[i] with { Status = status, Sources = slot.E[i].Sources.Append(source).ToArray() };
            }
        }
    }

    private static readonly SourceRef[] EventsSource =
    {
        new("model-recall", "Σύνταξη από γνώση του μοντέλου, από την αρχή. Η ημερομηνία και τα στοιχεία προς επαλήθευση από εγκυκλοπαιδικές, πανεπιστημιακές ή επίσημες πηγές.")
    };

    /// <summary>Reads the events files (events*.tsv): "MM-DD|year|region|title|summary"; the year may be empty for an annual observance (negative = BC).</summary>
    private void AddEvents(string file, List<string> errors)
    {
        var number = 0;
        foreach (var line in File.ReadLines(file))
        {
            number++;
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
            var f = line.Split('|');
            if (f.Length != 5 || f[0].Length != 5 || f[0][2] != '-'
                || !int.TryParse(f[0][..2], out var month) || !int.TryParse(f[0][3..], out var day)
                || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(2024, month)
                || (f[1].Length > 0 && !int.TryParse(f[1], out _)))
            {
                errors.Add($"{file}:{number}: bad events line");
                continue;
            }
            int? year = f[1].Length > 0 ? int.Parse(f[1], System.Globalization.CultureInfo.InvariantCulture) : null;
            if (!_days.TryGetValue((month, day), out var slot)) _days[(month, day)] = slot = (new(), new());
            slot.E.Add(new HistoricalEvent(year, f[3].Trim(), f[4].Trim(), null, f[2].Trim(), Verification.Unverified, EventsSource));
        }
    }

    private readonly Dictionary<(int Month, int Day), List<Person>> _births = new();

    /// <summary>
    /// Reads the births files: one line per person, "MM-DD|year|region|name|description"; lines starting with # are comments.
    /// A bad line is reported in <see cref="Errors"/> and skipped.
    /// </summary>
    private readonly Dictionary<(int Month, int Day), List<Person>> _deaths = new();

    private void AddBirths(string file, List<string> errors, Dictionary<(int Month, int Day), List<Person>> target, string kind)
    {
        var number = 0;
        foreach (var line in File.ReadLines(file))
        {
            number++;
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
            var f = line.Split('|');
            if (f.Length != 5 || f[0].Length != 5 || f[0][2] != '-'
                || !int.TryParse(f[0][..2], out var month) || !int.TryParse(f[0][3..], out var day)
                || !int.TryParse(f[1], out var year)
                || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(2024, month))
            {
                errors.Add($"{file}:{number}: bad {kind} line");
                continue;
            }
            if (!target.TryGetValue((month, day), out var list)) target[(month, day)] = list = new List<Person>();
            list.Add(new Person(year, f[3].Trim(), f[4].Trim(), f[2].Trim()));
        }
    }

    public DayView GetDay(int month, int day)
    {
        var births = _births.TryGetValue((month, day), out var b)
            ? b.OrderBy(p => Rank(p.Region, p.IsGreek)).ThenBy(p => p.Year < 0 ? int.MinValue + (-p.Year) : p.Year).ToList()
            : new List<Person>();
        var deaths = _deaths.TryGetValue((month, day), out var d)
            ? d.OrderBy(p => Rank(p.Region, p.IsGreek)).ThenBy(p => p.Year < 0 ? int.MinValue + (-p.Year) : p.Year).ToList()
            : new List<Person>();
        return _days.TryGetValue((month, day), out var s)
            ? new DayView(month, day, s.C, s.E.OrderBy(e => Rank(e.Region, e.IsGreek)).ThenBy(e => e.Year ?? int.MaxValue).ToList(), null, births, null, deaths)
            : new DayView(month, day, Array.Empty<Commemoration>(), Array.Empty<HistoricalEvent>(), null, births, null, deaths);
    }

    /// <summary>Gives every entry its stable identifier: the one of the registry file, or the one made from its content when it is not registered yet.</summary>
    private void ApplyIds(string dataRoot)
    {
        var registry = EntryIds.LoadRegistry(dataRoot);
        string Id(string kind, int month, int day, int? year, string title) =>
            registry.TryGetValue(EntryIds.Key(kind, month, day, year, title), out var id) ? id : EntryIds.Make(kind, month, day, year, title);
        foreach (var ((month, day), slot) in _days)
        {
            for (var i = 0; i < slot.C.Count; i++) slot.C[i] = slot.C[i] with { Id = Id("saint", month, day, null, slot.C[i].Title) };
            for (var i = 0; i < slot.E.Count; i++) slot.E[i] = slot.E[i] with { Id = Id("event", month, day, slot.E[i].Year, slot.E[i].Title) };
        }
        foreach (var ((month, day), list) in _births)
            for (var i = 0; i < list.Count; i++) list[i] = list[i] with { Id = Id("birth", month, day, list[i].Year, list[i].Name) };
        foreach (var ((month, day), list) in _deaths)
            for (var i = 0; i < list.Count; i++) list[i] = list[i] with { Id = Id("death", month, day, list[i].Year, list[i].Name) };
    }

    public int DeathCount => _deaths.Values.Sum(l => l.Count);
    public int DeathDayCount => _deaths.Count;

    public int BirthCount => _births.Values.Sum(l => l.Count);
    public int BirthDayCount => _births.Count;

    /// <summary>
    /// The day with the movable feasts of its year added. A feast marked TransfersAfterEaster (St George) moves to Easter Monday
    /// when its date falls on or before Easter Sunday.
    /// </summary>
    public DayView GetDay(DateOnly date)
    {
        var view = GetDay(date.Month, date.Day) with { Holidays = Core.Holidays.On(date, Region) };
        if (date.Year is < 1900 or > 2099) return view;
        view = view with { Fasting = Core.Fasting.For(date) };

        var easter = Paschalion.OrthodoxEaster(date.Year);
        var commemorations = view.Commemorations.ToList();
        commemorations.RemoveAll(c => c.TransfersAfterEaster && date <= easter);

        if (date == easter.AddDays(1))
        {
            foreach (var ((month, day), slot) in _days)
            {
                if (month == 2 && day == 29 && !DateTime.IsLeapYear(date.Year)) continue;   // 29 February exists only in leap years
                if (new DateOnly(date.Year, month, day) <= easter)
                    commemorations.AddRange(slot.C.Where(c => c.TransfersAfterEaster));
            }
        }

        commemorations.AddRange(Paschalion.Movable(date.Year, Region).Where(m => m.Date == date).Select(m => m.Feast));
        return view with { Commemorations = commemorations };
    }

    /// <summary>Every (month, day) on which a name (or one of its variants) celebrates; accents and case are ignored.</summary>
    public IReadOnlyList<(int Month, int Day, NameDay Name)> FindName(string name)
    {
        var key = TextNorm.Fold(name);
        var hits = new List<(int, int, NameDay)>();
        foreach (var ((m, d), slot) in _days.OrderBy(k => k.Key))
            foreach (var n in slot.C.SelectMany(c => c.Names))
                if (TextNorm.Fold(n.Name) == key || n.Variants.Any(v => TextNorm.Fold(v) == key))
                    hits.Add((m, d, n));
        return hits;
    }

    /// <summary>Every name of the calendar with the day it celebrates, in date order (a name with several days appears once per day).</summary>
    public IEnumerable<(int Month, int Day, NameDay Name)> AllNames() =>
        _days.OrderBy(k => k.Key).SelectMany(kv => kv.Value.C.SelectMany(c => c.Names).Select(n => (kv.Key.Month, kv.Key.Day, n)));

    public int DayCount => _days.Count;
    public IEnumerable<Commemoration> AllCommemorations => _days.Values.SelectMany(v => v.C);

    public IEnumerable<(int Month, int Day, Commemoration C)> AllCommemorationsByDay() => _days.OrderBy(k => k.Key).SelectMany(kv => kv.Value.C.Select(c => (kv.Key.Month, kv.Key.Day, c)));
    public IEnumerable<HistoricalEvent> AllEvents => _days.Values.SelectMany(v => v.E);
}
