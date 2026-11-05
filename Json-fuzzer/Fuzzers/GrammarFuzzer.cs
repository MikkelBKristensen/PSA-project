using Fuzzers.Grammar;

namespace Fuzzers;

/// <summary>
/// Grammar-based generator for JSON documents.
/// </summary>
/// <remarks>
/// Depth is the nesting level of containers (objects and arrays). The root object is at depth 1.
/// Once a value would be generated at <c>maxDepth</c>, only terminals (string, number, bool, null)
/// are chosen, so the recursion through <c>value -> array -> elements -> value</c> and
/// <c>value -> object -> members -> value</c> always terminates.
/// </remarks>
public static class GrammarFuzzer
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <param name="maxDepth">Maximum container nesting depth (root object counts as 1). Must be at least 1.</param>
    /// <param name="maxBreadth">Maximum number of members per object / elements per array.</param>
    /// <param name="seed">Seed for reproducible generation; <c>null</c> for a random seed.</param>
    public static string GenerateJson(int maxDepth = 4, int maxBreadth = 10, int? seed = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDepth, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(maxBreadth);

        var gen = new Generator(seed is null ? new Random() : new Random(seed.Value), maxDepth, maxBreadth);
        return gen.Object(depth: 1).ToString();
    }

    private sealed class Generator(Random rand, int maxDepth, int maxBreadth)
    {
        // `depth` is the depth of the container being generated.
        public JSON.Object Object(int depth)
        {
            var length = rand.NextInteger(0, maxBreadth + 1);
            var keyValues = new JSON.KeyValue[length];
            for (int i = 0; i < length; i++)
            {
                keyValues[i] = new JSON.KeyValue(rand.GetString(Alphabet, 15), Value(depth));
            }

            return new JSON.Object(new JSON.Members(keyValues));
        }

        private JSON.Array Array(int depth)
        {
            var length = rand.NextInteger(0, maxBreadth + 1);
            var values = new JSON.JsonValue[length];
            for (int i = 0; i < length; i++)
            {
                values[i] = Value(depth);
            }

            return new JSON.Array(new JSON.Elements(values));
        }

        // `parentDepth` is the depth of the container this value lives in.
        // A nested container would sit at parentDepth + 1, so it is only allowed below maxDepth.
        private JSON.JsonValue Value(int parentDepth)
        {
            bool canNest = parentDepth < maxDepth;
            int choices = canNest ? 7 : 5; // NextInteger's upper bound is exclusive

            return rand.NextInteger(0, choices) switch
            {
                0 => new JSON.Null(),
                1 => rand.GetString(Alphabet, rand.NextInteger(1, 10)),
                2 => rand.NextInteger(int.MinValue, int.MaxValue),
                3 => rand.NextInt64() * 1.0f,
                4 => rand.NextInteger(0, 2) == 1,
                5 => Array(parentDepth + 1),
                _ => Object(parentDepth + 1),
            };
        }
    }
}
