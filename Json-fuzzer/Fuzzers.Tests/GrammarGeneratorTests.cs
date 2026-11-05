using System.Text.Json;
using Fuzzers.Grammar;
using Xunit.Abstractions;

namespace Fuzzers.Tests;

public class GrammarGeneratorTests(ITestOutputHelper output)
{
    private const int Samples = 500;

    [Fact]
    public void PrintGrammarAndSample()
    {
        output.WriteLine(JsonGrammar.Create().ToString());
        output.WriteLine("");
        output.WriteLine(new GrammarGenerator(JsonGrammar.Create(), JsonGrammar.Options(3)).Generate(seed: 1));
    }

    [Fact]
    public void JsonGrammarOnlyProducesValidJson()
    {
        var gen = new GrammarGenerator(JsonGrammar.Create(), JsonGrammar.Options(4));
        for (int seed = 0; seed < Samples; seed++)
        {
            var json = gen.Generate(seed);
            try
            {
                using var _ = JsonDocument.Parse(json);
            }
            catch (Exception e)
            {
                Assert.Fail($"seed {seed}: {e.Message}\n{json}");
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void NestingNeverExceedsMaxDepth(int maxDepth)
    {
        var gen = new GrammarGenerator(JsonGrammar.Create(), JsonGrammar.Options(maxDepth));
        for (int seed = 0; seed < Samples; seed++)
        {
            var json = gen.Generate(seed);
            using var doc = JsonDocument.Parse(json);
            var depth = Depth(doc.RootElement);
            Assert.True(depth <= maxDepth, $"seed {seed}: depth {depth} > {maxDepth}\n{json}");
        }
    }

    [Fact]
    public void MaxDepthIsReached()
    {
        var gen = new GrammarGenerator(JsonGrammar.Create(), JsonGrammar.Options(4));
        Assert.Contains(Enumerable.Range(0, Samples),
            seed => Depth(JsonDocument.Parse(gen.Generate(seed)).RootElement) == 4);
    }

    [Fact]
    public void EveryValueKindIsGenerated()
    {
        var gen = new GrammarGenerator(JsonGrammar.Create(), JsonGrammar.Options(3));
        var kinds = Enumerable.Range(0, Samples)
            .SelectMany(seed => All(JsonDocument.Parse(gen.Generate(seed)).RootElement))
            .Select(e => e.ValueKind)
            .ToHashSet();

        Assert.Equal(Enum.GetValues<JsonValueKind>().Except([JsonValueKind.Undefined]).ToHashSet(), kinds);
    }

    [Fact]
    public void SameSeedGivesSameOutput()
    {
        var gen = new GrammarGenerator(JsonGrammar.Create(), JsonGrammar.Options());
        Assert.Equal(gen.Generate(42), gen.Generate(42));
    }

    [Fact]
    public void ExplodingGrammarStillTerminates()
    {
        // Each expansion makes on average 1.5 copies of itself, so without a node budget it would never finish.
        var grammar = new ContextFreeGrammar { ["<start>"] = [["x"], ["<start>", "<start>", "<start>"]] };
        var gen = new GrammarGenerator(grammar);
        for (int seed = 0; seed < 50; seed++)
            Assert.True(gen.Generate(seed).Length < 100_000);
    }

    [Fact]
    public void UnproductiveGrammarIsRejected()
    {
        var grammar = new ContextFreeGrammar { ["<start>"] = [["(", "<start>", ")"]] };
        Assert.False(GrammarGenerator.IsProductive(grammar));
        Assert.Throws<ArgumentException>(() => new GrammarGenerator(grammar));
    }

    [Fact]
    public void UnproductiveAlternativesAreNeverChosen()
    {
        var grammar = new ContextFreeGrammar
        {
            ["<start>"] = [["a"], ["<loop>"], ["<undefined>"]],
            ["<loop>"] = [["<loop>", "b"]],
        };
        var gen = new GrammarGenerator(grammar);
        Assert.All(Enumerable.Range(0, 50), seed => Assert.Equal("a", gen.Generate(seed)));
    }

    internal static int Depth(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => 1 + e.EnumerateObject().Select(p => Depth(p.Value)).DefaultIfEmpty(0).Max(),
        JsonValueKind.Array => 1 + e.EnumerateArray().Select(Depth).DefaultIfEmpty(0).Max(),
        _ => 0,
    };

    private static IEnumerable<JsonElement> All(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => e.EnumerateObject().SelectMany(p => All(p.Value)).Prepend(e),
        JsonValueKind.Array => e.EnumerateArray().SelectMany(All).Prepend(e),
        _ => [e],
    };
}
