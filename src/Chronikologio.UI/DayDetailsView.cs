using System.Globalization;
using System.Text;
using Chronikologio.Core;

namespace Chronikologio.UI;

/// <summary>Shows the saints, names, etymologies and history of one date. Short mode = summaries; full mode adds the long texts. Sources always close each text.</summary>
public sealed class DayDetailsView : RichTextBox
{
    /// <summary>False when the events are shown elsewhere (in the two frames with arrows): only the saints and the births are written here.</summary>
    public bool ShowEvents { get; set; } = true;

    public DayDetailsView()
    {
        ReadOnly = true;
        BorderStyle = BorderStyle.None;
        DetectUrls = true;
        LinkClicked += (_, e) =>
        {
            if (Uri.TryCreate(e.LinkText, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(u.AbsoluteUri) { UseShellExecute = true });
        };
    }

    public void ShowDay(DayStore store, DateOnly date, bool full)
    {
        var day = store.GetDay(date);
        Clear();
        Append(date.ToString("dddd d MMMM yyyy", Loc.Culture), 14f, FontStyle.Bold);
        if (OldCalendar.Note(date) is { Length: > 0 } oldNote) Append(oldNote, 10f, FontStyle.Regular);
        foreach (var h in day.HolidayList)
            Append(Holidays.Line(h) + (h.Note is { Length: > 0 } n ? " · " + n : ""), 10.5f, FontStyle.Bold);
        if (day.Fasting is not null) Append(day.Fasting, 10f, FontStyle.Italic);
        if (ShortText.NamesLine(day) is { Length: > 0 } celebrating) Append("\n" + celebrating, 11f, FontStyle.Bold);

        if (day.Commemorations.Count == 0 && day.Events.Count == 0 && day.People.Count == 0)
        {
            Append(Loc.T("details.empty"), 10f, FontStyle.Italic);
            return;
        }

        foreach (var c in day.Commemorations)
        {
            Append("\n" + c.Title, 11.5f, FontStyle.Bold);
            Append(c.Summary, 10f, FontStyle.Regular);
            if (full && !string.IsNullOrWhiteSpace(c.Details)) Append("\n" + c.Details, 10f, FontStyle.Regular);
            foreach (var n in c.Names)
                Append($"\n{n.Name} — {n.Etymology.Origin}: {n.Etymology.Meaning}" + (full && n.Etymology.Note is { } note ? $" ({note})" : ""), 9.5f, FontStyle.Italic);
            if (full) AppendSources(c.Sources, c.Status);
        }

        // Greek history first, then world history (DayStore already orders them that way).
        foreach (var (heading, events) in new[]
        {
            (Loc.TR("short.history.gr", store.Region), day.Events.Where(e => Countries.IsDomestic(store.Region, e)).ToList()),
            (Loc.T("short.history.world"), day.Events.Where(e => !Countries.IsDomestic(store.Region, e)).ToList())
        })
        {
            if (!ShowEvents || events.Count == 0) continue;
            Append("\n" + heading, 12.5f, FontStyle.Bold);
            foreach (var e in events)
            {
                Append("\n" + (e.Year is { } y ? $"{y} — " : "") + e.Title, 11.5f, FontStyle.Bold);
                Append(e.Summary, 10f, FontStyle.Regular);
                if (full && !string.IsNullOrWhiteSpace(e.Details)) Append("\n" + e.Details, 10f, FontStyle.Regular);
                if (full) AppendSources(e.Sources, e.Status);
            }
        }
        if (day.People.Count > 0)
        {
            Append(Loc.T("details.births"), 12.5f, FontStyle.Bold);
            foreach (var p in day.People)
                Append($"{p.YearText} — {p.Name}, {p.Description}", 10f, FontStyle.Regular);
        }
        if (day.DeathList.Count > 0)
        {
            Append(Loc.T("details.deaths"), 12.5f, FontStyle.Bold);
            foreach (var p in day.DeathList)
                Append($"{p.YearText} — {p.Name}, {p.Description}", 10f, FontStyle.Regular);
        }
        SelectionStart = 0;
        ScrollToCaret();
    }

    private void AppendSources(SourceRef[] sources, Verification status)
    {
        var sb = new StringBuilder(Loc.T("details.sources"));
        if (status == Verification.Unverified) sb.Append(Loc.T("details.unverified"));
        else if (status == Verification.Reviewed) sb.Append(Loc.T("details.reviewed"));
        else sb.Append(Loc.T("details.verified"));
        sb.Append(": ").Append(string.Join("  ·  ", sources.Select(Describe)));
        Append(sb.ToString(), 9f, FontStyle.Regular);
    }

    /// <summary>One source as text. A link to an open encyclopedia is only a pointer for "learn more", and is said so; official references come first.</summary>
    private static string Describe(SourceRef s) => s.Kind switch
    {
        "synaxarion" when s.Url is not null => Loc.T("details.synaxarion") + s.Url,
        "official-reference" when s.Url is not null => Loc.T("details.official") + s.Url,
        "encyclopedia-pointer" when s.Url is not null => Loc.T("details.pointer") + s.Url,
        _ => s.Url ?? s.Citation
    };

    private void Append(string text, float size, FontStyle style)
    {
        SelectionStart = TextLength;
        SelectionFont = new Font(Font.FontFamily, size, style);
        SelectionColor = ForeColor;
        AppendText(text + "\n");
    }
}
