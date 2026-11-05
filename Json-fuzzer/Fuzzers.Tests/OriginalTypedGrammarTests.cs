using System.Text.Json;
using Xunit.Abstractions;

namespace Fuzzers.Tests;

public class OrginalTypedGrammarTests(ITestOutputHelper outputHelper)
{
    private const int Samples = 500;

    [Fact]
    public void GenerateJsonObject()
    {
        var obj = GrammarFuzzer.GenerateJson();
        outputHelper.WriteLine(obj);
    }

    [Fact]
    public void TryParseOutputJson()
    {
        for (int seed = 0; seed < Samples; seed++)
        {
            var json = GrammarFuzzer.GenerateJson(seed: seed);
            try
            {
                JsonDocument.Parse(json);
            }
            catch (Exception e)
            {
                Assert.Fail($"seed {seed}: {e.Message}\n{json}");
            }
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    public void DepthNeverExceedsMax(int maxDepth)
    {
        for (int seed = 0; seed < Samples; seed++)
        {
            var json = GrammarFuzzer.GenerateJson(maxDepth: maxDepth, seed: seed);
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
            var depth = Depth(doc.RootElement);
            Assert.True(depth <= maxDepth, $"seed {seed}: depth {depth} > {maxDepth}\n{json}");
        }
    }

    [Fact]
    public void MaxDepthIsReached()
    {
        const int maxDepth = 4;
        var reached = Enumerable.Range(0, Samples)
            .Select(seed => GrammarFuzzer.GenerateJson(maxDepth: maxDepth, seed: seed))
            .Any(json => Depth(JsonDocument.Parse(json).RootElement) == maxDepth);
        Assert.True(reached);
    }

    [Fact]
    public void ArraysAndNestedObjectsAreGenerated()
    {
        bool sawArray = false, sawNestedObject = false;
        for (int seed = 0; seed < Samples && !(sawArray && sawNestedObject); seed++)
        {
            var root = JsonDocument.Parse(GrammarFuzzer.GenerateJson(maxDepth: 3, seed: seed)).RootElement;
            foreach (var prop in root.EnumerateObject())
            {
                sawArray |= prop.Value.ValueKind == JsonValueKind.Array;
                sawNestedObject |= prop.Value.ValueKind == JsonValueKind.Object;
            }
        }

        Assert.True(sawArray, "no arrays generated");
        Assert.True(sawNestedObject, "no nested objects generated");
    }

    [Fact]
    public void BothBooleansAreGenerated()
    {
        var all = string.Concat(Enumerable.Range(0, 50).Select(seed => GrammarFuzzer.GenerateJson(seed: seed)));
        Assert.Contains("true", all);
        Assert.Contains("false", all);
    }

    [Fact]
    public void SameSeedGivesSameOutput()
    {
        Assert.Equal(GrammarFuzzer.GenerateJson(seed: 42), GrammarFuzzer.GenerateJson(seed: 42));
    }

    /// <summary>Container nesting depth; a scalar is 0, an empty or flat container is 1.</summary>
    private static int Depth(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => 1 + e.EnumerateObject().Select(p => Depth(p.Value)).DefaultIfEmpty(0).Max(),
        JsonValueKind.Array => 1 + e.EnumerateArray().Select(Depth).DefaultIfEmpty(0).Max(),
        _ => 0,
    };
}
