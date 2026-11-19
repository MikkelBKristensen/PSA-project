using Fuzzers.Collections;
using Fuzzers.Config;

namespace Fuzzers.Grammar;

public static class JsonAsData
{
    private static string Expand(
            string startSymbol,
            int maxDepth,
            int maxWidth,
            int mutatedTerminals,
            Random rand,
            GeneratorConfig? config = null)
    {
        config ??= new GeneratorConfig();

        var options = Grammar(config)[startSymbol];
        var productions = options.RandomElementByWeight(option => option.Weight).Production;

        if (_terminals.Contains(startSymbol))
        {
            var terminal = productions[0];
            return terminal;
        }

        var production = string.Empty;

        foreach (var option in productions)
        {
            if (IsProduction(option))
            {
                // TODO: Implement the maxDepth stuff correctly
                // TODO: Implement mutatedTerminals
                production += Expand(option, maxDepth--, maxWidth--, mutatedTerminals, rand, config);
            }
            else
            {
                production += option;
            }
        }
        return production;
    }

    private static bool IsProduction(string s)
    {
        return s.StartsWith('<');
    }

    public static string GenerateJson(
            int maxDepth,
            int maxWidth,
            int mutatedTerminals,
            GeneratorConfig? config = null,
            int? seed = null)
    {
        var rand = seed != null ? new Random(seed.Value) : new Random();

        return Expand("<object>", maxDepth, maxWidth, mutatedTerminals, rand, config);
    }


    // Every printable ASCII character except '"' and '\', plus a few multi-byte UTF-8 characters.
    private static readonly string[] PlainCharacters =
    [
        .. Enumerable.Range(0x20, 0x7F - 0x20).Select(c => ((char)c).ToString()).Where(c => c is not "\"" and not "\\"),
        "é", "€", "😀",
    ];

    private static List<string> _terminals =
    [
        "<zero>", "<wschar>", "<onenine>", "<char>"
    ];

    private static Dictionary<string, List<(string[] Production, float Weight)>> Grammar(GeneratorConfig config) => new()
    {
        { "<object>", [
            (["{", "<members>", "}"], config.ObjectMembersWeight),
            (["{", "<ws>", "}"], config.ObjectEmptyWeight),
        ]},
        { "<members>", [
            (["<keyValue>"], config.MembersSingleWeight),
            (["<keyValue>", ",", "<ws>", "<members>"], config.MembersMultipleWeight),
        ]},
        { "<keyValue>", [
            (["<string>", "<ws>", ":", "<ws>", "<value>"], 1f),
        ]},
        // TODO: Reintroduce float as <value> option
        { "<value>", [
            (["<string>"], config.ValueStringWeight),
            (["<int>"], config.ValueIntWeight),
            (["<boolean>"], config.ValueBooleanWeight),
            (["<array>"], config.ValueArrayWeight),
            (["<object>"], config.ValueObjectWeight),
            (["null"], config.ValueNullWeight),
        ]},
        { "<array>", [
            (["[", "<ws>", "]"], config.ArrayEmptyWeight),
            (["[", "<elements>", "]"], config.ArrayElementsWeight),
        ]},
        { "<elements>", [
            (["<element>"], config.ElementsSingleWeight),
            (["<elements>", ",", "<elements>"], config.ElementsMultipleWeight),
        ]},
        { "<element>", [
            (["<ws>", "<value>", "<ws>"], 1f),
        ]},
        { "<string>", [
            (["\"", "<characters>", "\""], 1f),
        ]},
        { "<characters>", [
            (["<char>"], config.CharactersSingleWeight),
            (["<char>", "<characters>"], config.CharactersMultipleWeight),
        ]},
        { "<char>", [
            .. PlainCharacters.Select(c => (new[] { c }, config.CharWeight)),
        ]},
        { "<int>", [
            (["<digit>"], config.IntDigitWeight),
            (["-", "<digit>"], config.IntNegativeDigitWeight),
            (["-", "<digits>"], config.IntNegativeDigitsWeight),
            (["<digits>"], config.IntDigitsWeight),
        ]},
        { "<digits>", [
            (["<digit>"], config.DigitsSingleWeight),
            (["<onenine>", "<digits>"], config.DigitsMultipleWeight),
        ]},
        { "<digit>", [
            (["<zero>"], config.DigitZeroWeight),
            (["<onenine>"], config.DigitOneNineWeight),
        ]},
        { "<zero>", [
            (["0"], 1f),
        ]},
        { "<onenine>", [
            .. "123456789".Select(d => (new[] { d.ToString() }, config.OneNineWeight)),
        ]},
        { "<boolean>", [
            (["true"], config.BooleanTrueWeight),
            (["false"], config.BooleanFalseWeight),
        ]},
        //{ "<float>", [["<digit>"], ["-", "<digit>"], ["-", "<digits>"], ["digits"]] },
        //{ "<exponent>", ["1"] },
        { "<ws>", [
            ([], config.WsEmptyWeight),
            (["<wschar>"], config.WsSingleWeight),
            (["<wschar>", "<ws>"], config.WsMultipleWeight),
        ]},
        { "<wschar>", [
            ([""], config.WsCharEmptyWeight),
            ([" "], config.WsCharSpaceWeight),
            (["\n"], config.WsCharNewlineWeight),
            (["\t"], config.WsCharTabWeight),
        ]},
    };
}
