using System.Text;
using System.Text.RegularExpressions;

namespace Fuzzers.Harness;

/// <summary>
/// Turns parser outcomes into finding signatures. Inputs with the same signature are treated as the same bug
/// class, so a long run reports a list of distinct issues instead of millions of inputs.
/// </summary>
public static partial class Findings
{
    /// <summary>
    /// One signature per crashing parser, plus one if the parsers disagree on accept/reject.
    /// Empty if every parser agrees and none crashed.
    /// </summary>
    public static IEnumerable<string> Signatures(IReadOnlyList<IJsonParser> parsers, IReadOnlyList<Outcome> outcomes)
    {
        for (int i = 0; i < parsers.Count; i++)
        {
            if (outcomes[i].Verdict == Verdict.Crashed)
                yield return $"crash | {parsers[i].Name} | {outcomes[i].ErrorType} at {outcomes[i].Location ?? "?"}";
        }

        bool anyAccepted = outcomes.Any(o => o.Verdict == Verdict.Accepted);
        bool anyRejected = outcomes.Any(o => o.Verdict != Verdict.Accepted);
        if (anyAccepted && anyRejected)
        {
            var accepted = Enumerable.Range(0, parsers.Count).Where(i => outcomes[i].Verdict == Verdict.Accepted);
            var rejected = Enumerable.Range(0, parsers.Count).Where(i => outcomes[i].Verdict != Verdict.Accepted);
            yield return $"disagree | accepted by {string.Join(",", accepted.Select(i => parsers[i].Name))} | " +
                         string.Join(" ; ", rejected.Select(i => $"{parsers[i].Name}: {NormalizeMessage(outcomes[i].Message)}"));
        }
    }

    public static string Kind(string signature) => signature[..signature.IndexOf(' ')];

    /// <summary>
    /// Removes the input-specific parts of an error message (positions, paths, the offending character) so that
    /// e.g. "Unexpected character ... value: N. Path 'a', line 1, position 7." and the same error on another
    /// character at another position get the same signature.
    /// </summary>
    public static string NormalizeMessage(string? message)
    {
        if (message is null) return "";
        var m = message;
        foreach (var cut in new[] { " Path '", ", line ", " LineNumber:", " | BytePosition" })
        {
            int i = m.IndexOf(cut, StringComparison.Ordinal);
            if (i >= 0) m = m[..i];
        }

        m = QuotedCharRegex().Replace(m, "'_'"); // first, so a quoted quote ''' is handled
        m = QuotedRegex().Replace(m, "'_'");
        m = TrailingCharRegex().Replace(m, ": _.");
        m = DigitsRegex().Replace(m, "0");
        return Escape(m.Trim());
    }

    /// <summary>
    /// Makes a string safe to show and store: control characters, lone surrogates and line separators become
    /// <c>\uXXXX</c>. Valid surrogate pairs (e.g. emoji) are kept.
    /// </summary>
    public static string Escape(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                sb.Append(c).Append(s[++i]);
            }
            else if (c == '\\')
            {
                sb.Append(@"\\");
            }
            else if (char.IsControl(c) || char.IsSurrogate(c) || c is (char)0x2028 or (char)0x2029 or (char)0xFEFF or (char)0x00A0)
            {
                sb.Append($"\\u{(int)c:X4}");
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    [GeneratedRegex("'.'")]
    private static partial Regex QuotedCharRegex();

    [GeneratedRegex("'[^']*'")]
    private static partial Regex QuotedRegex();

    [GeneratedRegex(@": .\.$")]
    private static partial Regex TrailingCharRegex();

    // Numbers that stand alone (positions, values), not ones that are part of a name like UTF-16.
    [GeneratedRegex(@"(?<![A-Za-z\d-])\d+")]
    private static partial Regex DigitsRegex();
}
