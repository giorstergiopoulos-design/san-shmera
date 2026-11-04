using Microsoft.Win32;
using Opsis.UI;

namespace Chronikologio.UI;

/// <summary>
/// The colours of the app. The palettes are the shared ones of Opsis.UI (dark, light, Midnight, Forest, Dawn, Contrast); "system" follows
/// the Windows setting. Every control of Opsis.UI reads these colours through <see cref="OpsisTheme"/>, and every window of the app listens to <see cref="Changed"/>.
/// Inside Opsis the host pushes its own colours with <see cref="SetFromHost"/>.
/// </summary>
public static class ChronikologioTheme
{
    public static IReadOnlyList<string> Names { get; } = new[] { "system", "dark", "light", "midnight", "forest", "dawn", "contrast" };

    private static readonly Dictionary<string, string> Greek = new()
    {
        ["system"] = "Όπως τα Windows", ["dark"] = "Σκοτεινό", ["light"] = "Φωτεινό", ["midnight"] = "Μεσάνυχτα",
        ["forest"] = "Δάσος", ["dawn"] = "Αυγή", ["contrast"] = "Υψηλή αντίθεση", ["host"] = "Όπως το Opsis"
    };

    private static OpsisPalette _palette = OpsisPalette.Dark;

    /// <summary>Raised after the palette changed; windows restyle themselves and repaint.</summary>
    public static event Action? Changed;

    public static string Name { get; private set; } = "dark";
    public static OpsisPalette Palette => _palette;
    public static bool IsDark => _palette.IsDark;

    static ChronikologioTheme() => OpsisTheme.Use(new LiveTokens());

    public static string DisplayName(string name) => Greek.TryGetValue(name, out var g) ? g : name;

    /// <summary>Chooses a palette by name. An unknown name is refused (false) and nothing changes.</summary>
    public static bool Set(string? name)
    {
        var key = name?.Trim().ToLowerInvariant() ?? "";
        OpsisPalette? palette = key == "system"
            ? (SystemPrefersDark() ? OpsisPalette.Dark : OpsisPalette.Light)
            : OpsisThemes.TryGet(key);
        if (palette is null) return false;
        Name = key;
        _palette = palette;
        Notify();
        return true;
    }

    /// <summary>The next palette of <see cref="Names"/> after the current one (Ctrl+T).</summary>
    public static string Next()
    {
        var i = Names.ToList().IndexOf(Name);
        var next = Names[(i + 1) % Names.Count];
        Set(next);
        return next;
    }

    /// <summary>The colours of the host application (Opsis), pushed as ARGB integers.</summary>
    public static void SetFromHost(bool dark, int background, int surface, int border, int text, int textMuted, int accent)
    {
        _palette = new OpsisPalette(dark, Color.FromArgb(background), Color.FromArgb(surface), Color.FromArgb(border),
            Color.FromArgb(text), Color.FromArgb(textMuted), Color.FromArgb(accent));
        Name = "host";
        Notify();
    }

    public static bool SystemPrefersDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return true;
        }
    }

    private static void Notify()
    {
        OpsisTheme.NotifyChanged();
        Changed?.Invoke();
    }

    /// <summary>A live adapter: the controls read the tokens of whatever palette is current at paint time.</summary>
    private sealed class LiveTokens : IOpsisTheme
    {
        public bool IsDark => _palette.IsDark;
        public Color Background => _palette.Background;
        public Color Surface => _palette.Surface;
        public Color Border => _palette.Border;
        public Color Text => _palette.Text;
        public Color TextMuted => _palette.TextMuted;
        public Color Accent => _palette.Accent;
    }
}
