namespace Fuzzers.Grammar;

public static class JsonAsData
{

    private static string Expand(string startSymbol, int maxDepth, int maxWidth, int mutatedTerminals)
    {
        return "";
    }
    

    public static string GenerateJson(int maxDepth, int maxWidth, int mutatedTerminals)
    {
        
        
        
        
        
        
        return "";
    }
    
    
    // Every printable ASCII character except '"' and '\', plus a few multi-byte UTF-8 characters.
    private static readonly string[] PlainCharacters =
    [
        .. Enumerable.Range(0x20, 0x7F - 0x20).Select(c => ((char)c).ToString()).Where(c => c is not "\"" and not "\\"),
        "é", "€", "😀",
    ];
    
    
    private static readonly Dictionary<string, List<string[]>> grammar = new()
    {
        { "<object>", [["{","<members>","}"], ["{","<ws>","}"]] },
        { "<members>", [["<keyValue>"], ["<keyValue>","<ws>","<members>"]] },
        { "<keyValue>", [["<string>","<ws>",":","<ws>","<value>"]] },
        { "<value>", [["<string>"], ["<int>"], ["<float>"], ["<boolean>"], ["<array>"], ["<object>"], ["null"]] },
        { "<string>", [["\"", "<characters>", "\""]] },
        { "<characters>", [["<char>"], ["<char>", "<character>"]] },
        { "<char>", [[.. PlainCharacters.Select(c => c.ToString())]]},
        { "<int>", [["<digit>"], ["-", "<digit>"], ["-", "<digits>"], ["digits"]] },
        { "<digits>", [["<digit>"], ["<digit>","<digits>"]] },
        { "<digit>", [["0"], ["<onenine>"]] },
        { "<onenine>", [[.. "123456789".Select(d => d.ToString())]] },
        //{ "<float>", [["<digit>"], ["-", "<digit>"], ["-", "<digits>"], ["digits"]] },
        //{ "<exponent>", ["1"] },
        { "<ws>", [[], ["<wschar>"], ["<wschar>","<ws>"]]},
        { "<wschar>", [[""], [" "], ["\n"], ["\r"], ["\t"]] },
        { "<elements>", [["<element"], ["<elements>",",","<elements>"]]},
        { "<element>", [["<ws>","<value>","<ws>"]]},
        { "<array>", [["[", "<ws>", "]"], ["[", "<elements>", "]"]]},
    };
}