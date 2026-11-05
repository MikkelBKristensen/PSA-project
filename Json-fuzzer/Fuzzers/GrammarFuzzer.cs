using Fuzzers.Grammar;

namespace Fuzzers;

public static class GrammarFuzzer
{
    
    private static Random rand = new();
    private static string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    public static string GenerateJson(int maxNonTerminals=10, bool logging = false)
    {
        return ObjectGenerator().ToString();
    }

    private static JSON.Object ObjectGenerator()
    {
        var members = MemberGenerator();
        return new JSON.Object(members);
    }

    private static JSON.JsonValue ValueGenerator()
    {
        return rand.NextInteger(0, 5) switch
        {
            0 => new JSON.Null(),
            1 => rand.GetString(alphabet, rand.NextInteger(1, 10)),
            2 => rand.NextInteger(int.MinValue, int.MaxValue),
            3 => rand.NextInt64() * 1.0f,
            4 => rand.NextInteger(0, 1) == 1,
            5 => ArrayGenerator()
        };
    }

    private static JSON.Array ArrayGenerator()
    {
        var length = rand.NextInteger(100);
        JSON.JsonValue[] values = new JSON.JsonValue[length];
        for (int i = 0; i < length; i++)
        {
            values[i] = ValueGenerator();
        }

        var elements = new JSON.Elements(values);
        return new JSON.Array(elements);

    }

    private static JSON.KeyValue KeyValueGenerator()
    {
        return new JSON.KeyValue(rand.GetString(alphabet, 15), ValueGenerator());
    }


    private static JSON.Members MemberGenerator()
    {
        var length = rand.NextInteger(100);
        JSON.KeyValue[] keyValues = new JSON.KeyValue[length];
        for (int i = 0; i < length; i++)
        {
            keyValues[i] = KeyValueGenerator();
        }

        return new JSON.Members(keyValues);
    }
    
}