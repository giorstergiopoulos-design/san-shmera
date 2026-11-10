using System.Globalization;
using System.Text;

namespace Chronikologio.Core;

public static class TextNorm
{
    /// <summary>True when the text starts with the prefix and the prefix ends at the end of a word ("Άγιος Πλάτων" is not the beginning of "Άγιος Πλάτωνας").</summary>
    public static bool StartsWithWord(string text, string prefix) =>
        text.StartsWith(prefix, StringComparison.Ordinal) && (text.Length == prefix.Length || !char.IsLetter(text[prefix.Length]));

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
