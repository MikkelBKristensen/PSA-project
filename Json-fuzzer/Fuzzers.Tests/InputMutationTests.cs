using System.Text.Json;
using Fuzzers.Grammar;
using Fuzzers.Guidance;
using Fuzzers.Harness;
using Fuzzers.InputMutation;
using Xunit.Abstractions;

namespace Fuzzers.Tests;

public class InputMutationTests(ITestOutputHelper output)
{
    private static readonly ContextFreeGrammar Json = JsonGrammar.Create();
    private static readonly GrammarGenerator Generator = new(Json, JsonGrammar.Options(3));
    private static readonly TreeMutator Tree = new(Json, Generator, JsonGrammar.MutationTerminals);
    private static readonly CharMutator Char = new(JsonGrammar.MutationTerminals);

    [Fact]
    public void CharMutationIsDeterministicAndUsuallyChangesTheInput()
    {
        int changed = 0;
        for (int seed = 0; seed < 200; seed++)
        {
            var input = Generator.Generate(seed);
            var a = Char.Mutate(input, new Random(seed));
            var b = Char.Mutate(input, new Random(seed));
            Assert.Equal(a, b);
            if (a.Input != input) changed++;
        }

        Assert.True(changed > 180, $"only {changed}/200 changed");
    }

    [Theory]
    [InlineData(TreeMutator.Op.Regenerate)]
    [InlineData(TreeMutator.Op.Splice)] // without a donor this falls back to Regenerate
    public void RegeneratingSubtreesKeepsInputsValid(TreeMutator.Op op)
    {
        for (int seed = 0; seed < 300; seed++)
        {
            var rand = new Random(seed);
            var (tree, description) = Tree.MutateOnce(Generator.GenerateTree(rand), op, rand);
            var json = tree.ToString();
            try
            {
                using var _ = JsonDocument.Parse(json);
            }
            catch (JsonException e)
            {
                Assert.Fail($"seed {seed}, {description}: {e.Message}\n{json}");
            }
        }
    }

    [Fact]
    public void SpliceUsesTheDonor()
    {
        var rand = new Random(1);
        var donor = new DerivationTree("<value>", 6, [new DerivationTree("null", -1, [])]);
        var replaced = Enumerable.Range(0, 200)
            .Select(_ => Tree.MutateOnce(Generator.GenerateTree(rand), TreeMutator.Op.Splice, rand,
                sym => sym == "<value>" ? donor : null))
            .Count(r => r.Tree.Descendants().Contains(donor));
        Assert.True(replaced > 0);
    }

    [Theory]
    [InlineData(TreeMutator.Op.Delete)]
    [InlineData(TreeMutator.Op.Duplicate)]
    [InlineData(TreeMutator.Op.Swap)]
    [InlineData(TreeMutator.Op.ReplaceNonTerminal)]
    [InlineData(TreeMutator.Op.ReplaceToken)]
    [InlineData(TreeMutator.Op.InsertToken)]
    public void StructuralMutationsMarkTheEditedNode(TreeMutator.Op op)
    {
        var rand = new Random(3);
        var original = Generator.GenerateTree(rand);
        var (mutated, description) = Tree.MutateOnce(original, op, rand);
        output.WriteLine(description);
        output.WriteLine(original.ToString());
        output.WriteLine(mutated.ToString());

        Assert.Contains(mutated.Descendants(), n => n.Alternative == TreeMutator.Mutated);
        // The original tree is not modified.
        Assert.DoesNotContain(original.Descendants(), n => n.Alternative == TreeMutator.Mutated);
    }

    [Fact]
    public void KPathsAreStableAndDistinguishStructure()
    {
        HashSet<int> Of(DerivationTree t)
        {
            var set = new HashSet<int>();
            Features.KPaths(t, 3, set);
            return set;
        }

        var rand = new Random(1);
        var a = Generator.GenerateTree(rand);
        Assert.True(Of(a).SetEquals(Of(a)));
        Assert.NotEmpty(Of(a));

        // Every structural mutation adds a path through the edited node that the original did not have.
        foreach (var op in new[] { TreeMutator.Op.Delete, TreeMutator.Op.Duplicate, TreeMutator.Op.InsertToken })
        {
            var (mutated, description) = Tree.MutateOnce(a, op, rand);
            output.WriteLine(description);
            Assert.False(Of(mutated).IsSubsetOf(Of(a)));
        }
    }

    [Fact]
    public void CorpusOnlyKeepsInputsWithNewFeatures()
    {
        var corpus = new Corpus();
        var rand = new Random(0);
        Assert.True(corpus.TryAdd("a", null, [1, 2], rand));
        Assert.False(corpus.TryAdd("b", null, [1, 2], rand));
        Assert.True(corpus.TryAdd("c", null, [2, 3], rand));
        Assert.Equal(2, corpus.Count);
        Assert.Equal(3, corpus.FeatureCount);
        Assert.Contains(corpus.Pick(rand).Input, new[] { "a", "c" });
    }

    [Theory]
    [MemberData(nameof(StrategyNames))]
    public void EveryStrategyProducesInputs(string name)
    {
        var source = InputSources.Create(name, maxDepth: 3);
        var parsers = JsonParsers.All();
        var rand = new Random(0);
        int produced = 0;
        for (int i = 0; i < 500; i++)
        {
            if (source.Next(rand) is not { } next) continue;
            produced++;
            source.Feedback(parsers, parsers.Select(p => p.Parse(next.Input)).ToArray());
        }

        output.WriteLine($"{name}: {produced}/500 inputs; metrics: " +
                         string.Join(", ", source.Metrics?.Select(kv => $"{kv.Key}={kv.Value}") ?? []));
        Assert.True(produced > 0);
    }

    [Theory]
    [InlineData("tree-mutation-guided")]
    [InlineData("char-mutation-guided")]
    public void GuidedCorpusGrowsBeyondTheSeeds(string name)
    {
        var source = InputSources.Create(name, maxDepth: 3);
        var parsers = JsonParsers.All();
        var rand = new Random(0);
        for (int i = 0; i < 3000; i++)
        {
            if (source.Next(rand) is not { } next) continue;
            source.Feedback(parsers, parsers.Select(p => p.Parse(next.Input)).ToArray());
        }

        var corpus = source.Metrics!["corpus"];
        output.WriteLine($"{name}: corpus {corpus}, features {source.Metrics["features"]}");
        Assert.True(corpus > 100);
    }

    public static TheoryData<string> StrategyNames() => new(InputSources.Names);
}
