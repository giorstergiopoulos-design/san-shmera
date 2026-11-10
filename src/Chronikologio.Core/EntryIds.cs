using System.Security.Cryptography;
using System.Text;

namespace Chronikologio.Core;

/// <summary>
/// Stable identifiers of the entries (saints, events, births, deaths): <c>kind:MM-DD:year:hash</c>, e.g. <c>event:12-06:1917:3fa9c01b</c>.
/// The identifier is made once from the entry and then kept in the registry file <c>data/ids/ids.tsv</c>, so that correcting a title later does not
/// change what other files (translations, images, verification) point at. The registry is written by the test <c>DumpIdRegistry</c> (CHRON_IDS=file).
/// </summary>
public static class EntryIds
{
    public const string RegistryFile = "ids.tsv";

    private static string Norm(string title) => System.Text.RegularExpressions.Regex.Replace(TextNorm.Fold(title), @"\s+", " ").Trim();

    /// <summary>The identifier an entry gets when it is not in the registry yet.</summary>
    public static string Make(string kind, int month, int day, int? year, string title)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Norm(title))))[..8].ToLowerInvariant();
        return $"{kind}:{month:00}-{day:00}:{(year is { } y ? y.ToString(System.Globalization.CultureInfo.InvariantCulture) : "x")}:{hash}";
    }

    /// <summary>The key by which the registry finds an entry: kind, date, year and the folded title.</summary>
    public static string Key(string kind, int month, int day, int? year, string title) =>
        $"{kind}|{month:00}-{day:00}|{(year is { } y ? y.ToString(System.Globalization.CultureInfo.InvariantCulture) : "")}|{Norm(title)}";

    /// <summary>Reads the registry: key → identifier. A missing file gives an empty registry.</summary>
    public static Dictionary<string, string> LoadRegistry(string dataRoot)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.Exists(dataRoot) ? Directory.EnumerateFiles(dataRoot, RegistryFile, SearchOption.AllDirectories) : Array.Empty<string>())
            foreach (var line in File.ReadLines(file))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#')) continue;
                var f = line.Split('|');
                // id|kind|MM-DD|year|title
                if (f.Length < 5 || f[2].Length != 5 || f[2][2] != '-') continue;
                if (!int.TryParse(f[2][..2], out var m) || !int.TryParse(f[2][3..], out var d)) continue;
                int? year = int.TryParse(f[3], out var y) ? y : null;
                map[Key(f[1], m, d, year, string.Join("|", f.Skip(4)))] = f[0];
            }
        return map;
    }
}
