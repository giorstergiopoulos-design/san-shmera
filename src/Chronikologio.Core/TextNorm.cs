using System.Globalization;
using System.Text;

namespace Chronikologio.Core;

public static class TextNorm
{
    /// <summary>Lower case without accents or diaeresis, final sigma folded, so "Νεκτάριος" matches "νεκταριος".</summary>
    public static string Fold(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(ch == 'ς' ? 'σ' : char.ToLowerInvariant(ch));
        }
        return sb.ToString().Trim();
    }
}
