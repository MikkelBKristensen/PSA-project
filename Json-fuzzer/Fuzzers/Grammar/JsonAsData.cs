using System.Text.Json;
using Fuzzers.Collections;
using Fuzzers.Config;
using Fuzzers.Util;

namespace Fuzzers.Grammar;

public static class JsonAsData
{
    private static string Expand(
            string startSymbol,
            int maxDepth,
            int maxWidth, // TODO ¿implement?
            int mutatedTerminals,
            Random rand,
            GeneratorConfig? config = null)
    {
        config ??= new GeneratorConfig();

        if (maxDepth <= 0)
        {
            config = config with
            {
                ValueObjectWeight = 0
            };
        }

        var options = Grammar(config)[startSymbol];
        var productions = options.RandomElementByWeight(option => option.Weight, rand).Production;

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
                if (option == "<object>")
                {
                    maxDepth--;
                }

                // TODO: Implement the maxDepth stuff correctly
                // TODO: Implement mutatedTerminals
                production += Expand(option, maxDepth, maxWidth--, mutatedTerminals, rand, config);
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
            int maxWidth,
            int mutatedTerminals,
            GeneratorConfig config,
            int? seed = null)
    {
        if (config.MaxDepth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(config), "MaxDepth must larger than 0");
        }

        if (config.MaxWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(config), "MaxWidth must larger than 0");
        }

        var rand = seed != null ? new Random(seed.Value) : new Random();

        return Expand("<object>", config.MaxDepth, maxWidth, mutatedTerminals, rand, config);
    }

    private static readonly List<string> _terminals =
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
            (["<element>", ",", "<elements>"], config.ElementsMultipleWeight),
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
            .. Alphabet.PlainCharacters.Select(c => (new[] { c }, config.CharWeight)),
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
