using System.Text.Json.Serialization;

namespace Chronikologio.Core;

/// <summary>How far a record has been checked against an authoritative source. Everything starts as Unverified.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Verification { Unverified, Reviewed, Verified }

/// <summary>
/// Where a statement comes from. Kind: "model-recall", "church", "encyclopedia", "university", "academy", "primary".
/// Url is filled in only after the page was opened and checked; never guessed. Wikipedia and other user-editable sites are not accepted.
/// </summary>
public sealed record SourceRef(string Kind, string Citation, string? Url = null);

/// <summary>
/// An icon or photograph. File is relative to data/images. Only images from official bodies (museums, archives, universities, the Church)
/// under a licence that allows reuse are accepted; Creator, License and SourceUrl are mandatory and shown next to the image.
/// Kind: "icon" (saint), "portrait" (person), "photo" (event).
/// </summary>
public sealed record ImageRef(string File, string Caption, string Creator, string License, string SourceUrl, string? Kind = null);

/// <summary>Origin and meaning of a name. Origin e.g. "ελληνική", "εβραϊκή", "λατινική"; Note records scholarly doubt.</summary>
public sealed record Etymology(string Origin, string Meaning, string? Note = null);

/// <summary>A name that celebrates on the day, with its etymology. Variants are other forms (Κοσμάς, Κοσμούλα...).</summary>
public sealed record NameDay(string Name, string[] Variants, Etymology Etymology);

/// <summary>
/// A saint, group of saints or feast of a tradition on a fixed date.
/// Summary is the short life (2-3 sentences, shown in the widget and the bars); Details is the full text, written from scratch
/// (paragraphs separated by a blank line) and never copied from a source. Sources are listed after the text.
/// </summary>
public sealed record Commemoration(
    string Title,
    string Summary,
    string? Details,
    NameDay[] Names,
    Verification Status,
    SourceRef[] Sources,
    bool IsMajorFeast = false,
    ImageRef[]? Images = null,
    bool TransfersAfterEaster = false,
    string? Id = null);

/// <summary>A historical event of the day. Year is null for an annual observance. Summary/Details follow the same rules as for a commemoration.</summary>
public sealed record HistoricalEvent(
    int? Year,
    string Title,
    string Summary,
    string? Details,
    string Region,
    Verification Status,
    SourceRef[] Sources,
    ImageRef[]? Images = null,
    string? Id = null)
{
    private static readonly HashSet<string> GreekRegions = new(StringComparer.Ordinal)
    {
        "Ελλάδα", "Κύπρος", "Κωνσταντινούπολη", "Μικρά Ασία", "Θεσσαλονίκη", "Κρήτη", "Πόντος", "Ήπειρος", "Μακεδονία", "Θράκη"
    };

    /// <summary>Events of Greek history (Greece, Cyprus, the Greek world) are listed before those of world history.</summary>
    [JsonIgnore]
    public bool IsGreek => GreekRegions.Contains(Region);

    [JsonIgnore]
    public bool IsCypriot => Region == "Κύπρος";
}

/// <summary>A notable person born on the day. Persons of the Greek world (Region "Ελλάδα") are listed before the others. Year is negative for BC.</summary>
public sealed record Person(int Year, string Name, string Description, string Region, ImageRef[]? Images = null, string? Id = null)
{
    public bool IsGreek => Region is "Ελλάδα" or "Κύπρος";

    public bool IsCypriot => Region == "Κύπρος";

    /// <summary>"1879" or "100 π.Χ.".</summary>
    public string YearText => Year < 0 ? $"{-Year} π.Χ." : Year.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>Sources to add to the events of the same day and year (so a hand-written event can get documentation without being edited).</summary>
public sealed record EventSourceSupplement(int Year, SourceRef[] Sources);

public sealed record DayRecord(int Day, Commemoration[]? Commemorations = null, HistoricalEvent[]? Events = null, EventSourceSupplement[]? SourceSupplements = null);

/// <summary>One data file: a month of one tradition ("orthodox-el", "history", ...). Calendar is the calendar the dates are in.</summary>
/// <summary>DefaultSources apply to every record of the file that lists no sources of its own (so a file does not repeat the same citation hundreds of times).</summary>
public sealed record MonthFile(string Tradition, string Calendar, int Month, DayRecord[] Days, SourceRef[]? DefaultSources = null);

/// <summary>Everything known for one date, merged from all loaded traditions.</summary>
public sealed record DayView(int Month, int Day, IReadOnlyList<Commemoration> Commemorations, IReadOnlyList<HistoricalEvent> Events, string? Fasting = null, IReadOnlyList<Person>? Births = null, IReadOnlyList<Holiday>? Holidays = null, IReadOnlyList<Person>? Deaths = null)
{
    /// <summary>The holidays of the date (national feast days, public holidays, days of memory, local holidays). Known only for a day asked with its year.</summary>
    public IReadOnlyList<Holiday> HolidayList => Holidays ?? Array.Empty<Holiday>();

    /// <summary>Persons born on the day, Greek first.</summary>
    public IReadOnlyList<Person> People => Births ?? Array.Empty<Person>();

    /// <summary>Persons who died on the day, Greek first.</summary>
    public IReadOnlyList<Person> DeathList => Deaths ?? Array.Empty<Person>();

    public IEnumerable<NameDay> Names => Commemorations.SelectMany(c => c.Names);

    /// <summary>The one large picture of the day: the icon of the saint celebrated (a major feast first). Event photographs stay in the event frames, portraits in the births.</summary>
    public IEnumerable<ImageRef> SaintImages =>
        Commemorations.OrderBy(c => c.IsMajorFeast ? 0 : 1).SelectMany(c => c.Images ?? Array.Empty<ImageRef>()).Take(1);

    /// <summary>All images of the day: icons of the saints first, then the event photographs.</summary>
    public IEnumerable<ImageRef> Images =>
        Commemorations.SelectMany(c => c.Images ?? Array.Empty<ImageRef>())
            .Concat(Events.SelectMany(e => e.Images ?? Array.Empty<ImageRef>()))
            .Concat(People.SelectMany(p => p.Images ?? Array.Empty<ImageRef>()));
}
