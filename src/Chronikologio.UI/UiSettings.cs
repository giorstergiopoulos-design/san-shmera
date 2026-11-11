using System.Text.Json;
using Chronikologio.Core;

namespace Chronikologio.UI;

/// <summary>What the user chose about the look of the app: the theme and whether the menu bar is shown (Ctrl+M). Stored in %AppData%\Chronikologio\settings.json.</summary>
public sealed class UiSettings
{
    public string Theme { get; set; } = "system";
    public bool MenuShown { get; set; }

    /// <summary>The stand-alone app looks for a newer version in the official releases (at most once a day). The Opsis plugin never does: Opsis updates its plugins.</summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>The way a day is shown: "day" (the default), "cards", "feed" or "tabs".</summary>
    public string Layout { get; set; } = "day";

    /// <summary>"gr" = the calendar of the Church of Greece (default), "cy" = the calendar of the Church of Cyprus.</summary>
    public string Region { get; set; } = "gr";

    /// <summary>The language of the interface: "auto" (the language of the country, the default), "el" or "en". Independent of the country when chosen.</summary>
    public string Language { get; set; } = "auto";

    /// <summary>Size of the window when it was last closed (client area), and whether it was maximised.</summary>
    public int Width { get; set; } = 1120;
    public int Height { get; set; } = 740;
    public bool Maximized { get; set; }

    /// <summary>The smallest window that still fits the three layouts (rail, two cards and the small calendar).</summary>
    public const int MinWidth = 880;
    public const int MinHeight = 580;

    public static readonly IReadOnlyList<string> LayoutNames = new[] { "day", "cards", "feed", "tabs" };

    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Chronikologio", "settings.json");

    /// <summary>The saved settings, or the defaults when the file is missing or damaged.</summary>
    public static UiSettings Load(string? path = null)
    {
        try
        {
            path ??= DefaultPath;
            if (!File.Exists(path)) return new UiSettings();
            var s = JsonSerializer.Deserialize<UiSettings>(File.ReadAllText(path)) ?? new UiSettings();
            if (!ChronikologioTheme.Names.Contains(s.Theme)) s.Theme = "system";
            if (!DayStore.Regions.Contains(s.Region)) s.Region = "gr";
            if (!LayoutNames.Contains(s.Layout)) s.Layout = "day";
            if (s.Language is not ("auto" or "el" or "en")) s.Language = "auto";
            s.Width = Math.Clamp(s.Width, MinWidth, 4000);
            s.Height = Math.Clamp(s.Height, MinHeight, 3000);
            return s;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new UiSettings();
        }
    }

    public void Save(string? path = null)
    {
        try
        {
            path ??= DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // the settings are a convenience: failing to save must never stop the app
        }
    }
}
