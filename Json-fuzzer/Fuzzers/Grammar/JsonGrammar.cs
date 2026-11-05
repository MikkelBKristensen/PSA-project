namespace Fuzzers.Grammar;

/// <summary>
/// JSON as a <see cref="ContextFreeGrammar"/>, following the McKeeman form on json.org (RFC 8259).
/// </summary>
public static class JsonGrammar
{
    /// <summary>Nonterminals that count towards nesting depth: depth = how deeply objects/arrays are nested.</summary>
    public static readonly IReadOnlySet<string> NestingSymbols = new HashSet<string> { "<object>", "<array>" };

    /// <summary>Generator options where <paramref name="maxDepth"/> bounds object/array nesting.</summary>
    public static GeneratorOptions Options(int maxDepth = 4) =>
        new() { MaxDepth = maxDepth, DepthSymbols = NestingSymbols };

    /// <summary>
    /// Terminals used by the ReplaceSymbol and AddAlternative mutation operators: JSON punctuation and
    /// keywords, plus characters and tokens that are close to, but not, valid JSON.
    /// </summary>
    public static readonly IReadOnlyList<string> MutationTerminals =
    [
        // JSON's own tokens
        "{", "}", "[", "]", ",", ":", "\"", "\\", "true", "false", "null", "-", "+", ".", "e", "0", "u",
        // near-misses from other JSON dialects (JSON5, JavaScript, Python)
        "'", "NaN", "Infinity", "undefined", "None", "//", "/*", "0x",
        // awkward characters
        .. new[] { 0x0000, 0x001F, 0x007F, 0x000B, 0x00A0, 0xFEFF, 0x2028, 0xD800 }.Select(c => ((char)c).ToString()),
    ];

    // Every printable ASCII character except '"' and '\', plus a few multi-byte UTF-8 characters.
    private static readonly string[] PlainCharacters =
    [
        .. Enumerable.Range(0x20, 0x7F - 0x20).Select(c => ((char)c).ToString()).Where(c => c is not "\"" and not "\\"),
        "é", "€", "😀",
    ];

    public static ContextFreeGrammar Create() => new("<start>")
    {
        ["<start>"] = [["<element>"]],
        ["<value>"] = [["<object>"], ["<array>"], ["<string>"], ["<number>"], ["true"], ["false"], ["null"]],
        ["<object>"] = [["{", "<ws>", "}"], ["{", "<members>", "}"]],
        ["<members>"] = [["<member>"], ["<member>", ",", "<members>"]],
        ["<member>"] = [["<ws>", "<string>", "<ws>", ":", "<element>"]],
        ["<array>"] = [["[", "<ws>", "]"], ["[", "<elements>", "]"]],
        ["<elements>"] = [["<element>"], ["<element>", ",", "<elements>"]],
        ["<element>"] = [["<ws>", "<value>", "<ws>"]],
        ["<string>"] = [["\"", "<characters>", "\""]],
        ["<characters>"] = [[], ["<character>", "<characters>"]],
        ["<character>"] = [.. PlainCharacters.Select(c => new[] { c }), ["\\", "<escape>"]],
        ["<escape>"] = [["\""], ["\\"], ["/"], ["b"], ["f"], ["n"], ["r"], ["t"], ["u", "<hex>", "<hex>", "<hex>", "<hex>"]],
        ["<hex>"] = [["<digit>"], .. "ABCDEFabcdef".Select(c => new[] { c.ToString() })],
        ["<number>"] = [["<integer>", "<fraction>", "<exponent>"]],
        ["<integer>"] = [["<digit>"], ["<onenine>", "<digits>"], ["-", "<digit>"], ["-", "<onenine>", "<digits>"]],
        ["<digits>"] = [["<digit>"], ["<digit>", "<digits>"]],
        ["<digit>"] = [["0"], ["<onenine>"]],
        ["<onenine>"] = [.. "123456789".Select(c => new[] { c.ToString() })],
        ["<fraction>"] = [[], [".", "<digits>"]],
        ["<exponent>"] = [[], ["E", "<sign>", "<digits>"], ["e", "<sign>", "<digits>"]],
        ["<sign>"] = [[], ["+"], ["-"]],
        // json.org writes ws ::= "" | ' ' ws | '\n' ws | ...; splitting out <wschar> keeps the same
        // language but makes whitespace runs short (mean length 1 instead of 4).
        ["<ws>"] = [[], ["<wschar>", "<ws>"]],
        ["<wschar>"] = [[" "], ["\n"], ["\r"], ["\t"]],
    };
}
