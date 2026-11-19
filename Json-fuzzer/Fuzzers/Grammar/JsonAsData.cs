namespace Fuzzers.Grammar;

public static class JsonAsData
{
    private static string Expand(string startSymbol, int maxDepth, int maxWidth, int mutatedTerminals, Random rand)
    {
        var options = Grammar[startSymbol];
        var index = rand.Next(options.Count);
        var productions = options[index];
        
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
                production += Expand(option, maxDepth--, maxWidth--, mutatedTerminals, rand);
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

    public static string GenerateJson(int maxDepth, int maxWidth, int mutatedTerminals, int? seed = null)
    {
        var rand = seed != null ? new Random(seed.Value) : new Random();
        
        return Expand("<object>", maxDepth, maxWidth, mutatedTerminals, rand);
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
    
    private static readonly Dictionary<string, List<string[]>> Grammar = new()
    {
        { "<object>", [["{","<members>","}"], ["{","<ws>","}"]] },
        { "<members>", [["<keyValue>"], ["<keyValue>", ",", "<ws>","<members>"]] },
        { "<keyValue>", [["<string>","<ws>",":","<ws>","<value>"]] },
        // TODO: Reintroduce float as <value> option
        { "<value>", [["<string>"], ["<int>"], ["<boolean>"], ["<array>"], ["<object>"], ["null"]] },
        { "<array>", [["[", "<ws>", "]"], ["[", "<elements>", "]"]]},
        { "<elements>", [["<element>"], ["<elements>",",","<elements>"]]},
        { "<element>", [["<ws>","<value>","<ws>"]]},
        { "<string>", [["\"", "<characters>", "\""]] },
        { "<characters>", [["<char>"], ["<char>", "<characters>"]] },
        { "<char>", [.. PlainCharacters.Select(c => new[] { c.ToString() })] },
        { "<int>", [["<digit>"], ["-", "<digit>"], ["-", "<digits>"], ["<digits>"]] },
        { "<digits>", [["<digit>"], ["<onenine>","<digits>"]] },
        { "<digit>", [["<zero>"], ["<onenine>"]] },
        { "<zero>", [["0"]]},
        { "<onenine>", [.. "123456789".Select(d => new[] { d.ToString() })] },
        { "<boolean>", [["true"], ["false"]]},
        //{ "<float>", [["<digit>"], ["-", "<digit>"], ["-", "<digits>"], ["digits"]] },
        //{ "<exponent>", ["1"] },
        { "<ws>", [[], ["<wschar>"], ["<wschar>","<ws>"]]},
        { "<wschar>", [[""], [" "], ["\n"], ["\t"]] },
    };
}