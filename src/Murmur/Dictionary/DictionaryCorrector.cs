using System.Text.RegularExpressions;

namespace Murmur.Dictionary;

/// <summary>
/// Applies user-defined term corrections to recognized text after the fact.
/// Neither SAPI's DictationGrammar nor Vosk's default model exposes a way to
/// bias recognition toward specific technical terms at decode time, so
/// "force correct transcription of technical terms" is implemented as a
/// post-recognition find-and-replace instead: each entry's Aliases (common
/// mis-recognitions) are replaced with its correct Term, matched as whole
/// words, case-insensitively.
/// </summary>
public static class DictionaryCorrector
{
    public static string Apply(string text, IReadOnlyList<DictionaryEntry> entries)
    {
        if (string.IsNullOrEmpty(text) || entries.Count == 0)
        {
            return text;
        }

        // Longest alias first so a multi-word alias isn't pre-empted by a
        // shorter one that happens to be a substring of it.
        var replacements = entries
            .SelectMany(entry => entry.Aliases.Select(alias => (alias, entry.Term)))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.alias))
            .OrderByDescending(pair => pair.alias.Length);

        foreach (var (alias, term) in replacements)
        {
            text = Regex.Replace(text, $@"\b{Regex.Escape(alias)}\b", term, RegexOptions.IgnoreCase);
        }

        return text;
    }
}
