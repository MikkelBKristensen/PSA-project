using System.Diagnostics;
using System.Text.Json;
using Fuzzers.Grammar;
using Xunit.Abstractions;

namespace Fuzzers.Tests;

public class GrammarMutatorTests(ITestOutputHelper output)
{
    private static readonly ContextFreeGrammar Json = JsonGrammar.Create();
    private static readonly MutatorOptions JsonMutation = new() { Terminals = JsonGrammar.MutationTerminals };

    [Fact]
    public void MutantsAreDistinctProductiveAndGenerate()
    {
        var mutants = GrammarMutator.Mutants(Json, JsonMutation).ToList();

        foreach (var group in mutants.GroupBy(m => m.Mutation.Operator))
            output.WriteLine($"{group.Key,-18} {group.Count(),6} mutants");
        output.WriteLine($"{"total",-18} {mutants.Count,6}");

        Assert.Equal(mutants.Count, mutants.Select(m => m.Grammar.ToString()).Distinct().Count());
        Assert.DoesNotContain(mutants, m => m.Grammar.ToString() == Json.ToString());
        Assert.All(mutants, m => new GrammarGenerator(m.Grammar, JsonGrammar.Options(3)).Generate(seed: 0));
    }

    [Fact]
    public void DerivationTreeRecordsChosenAlternatives()
    {
        var tree = new GrammarGenerator(Json, JsonGrammar.Options(3)).GenerateTree(new Random(7));
        Assert.All(tree.Descendants(), n =>
        {
            if (n.IsTerminal) Assert.Equal(-1, n.Alternative);
            else Assert.Equal(Json[n.Symbol][n.Alternative].Length, n.Children.Count);
        });
    }

    [Fact]
    public void DeletingTheColonBreaksEveryInputThatExercisesIt()
    {
        // <member> ::= <ws> <string> <ws> ':' <element>   ⇒   <ws> <string> <ws> <element>
        var mutation = new GrammarMutation(MutationOperator.DeleteSymbol, "<member>", 0, 3);
        output.WriteLine(mutation.Describe(Json));
        var gen = new GrammarGenerator(mutation.Apply(Json), JsonGrammar.Options(3));

        var trees = Enumerable.Range(0, 200).Select(seed => gen.GenerateTree(new Random(seed))).ToList();
        var exercised = trees.Where(mutation.IsExercisedBy).ToList();

        // <member> has a single alternative, so "exercised" is exactly "contains a member".
        Assert.All(trees, t => Assert.Equal(t.Contains("<member>"), mutation.IsExercisedBy(t)));
        Assert.NotEmpty(exercised);
        Assert.All(exercised, tree => Assert.ThrowsAny<JsonException>(() => JsonDocument.Parse(tree.ToString())));
    }

    [Fact]
    public void AddingNaNAsValueIsRejectedWhenExercised()
    {
        var mutation = new GrammarMutation(MutationOperator.AddAlternative, "<value>", 7, Symbol: "NaN");
        output.WriteLine(mutation.Describe(Json));
        var gen = new GrammarGenerator(mutation.Apply(Json), JsonGrammar.Options(3));

        var exercised = Enumerable.Range(0, 200)
            .Select(seed => gen.GenerateTree(new Random(seed)))
            .Where(mutation.IsExercisedBy)
            .ToList();

        Assert.NotEmpty(exercised);
        Assert.All(exercised, tree => Assert.Contains("NaN", tree.ToString()));
        Assert.All(exercised, tree => Assert.ThrowsAny<JsonException>(() => JsonDocument.Parse(tree.ToString())));
    }

    [Fact]
    public void FocusMakesTheMutationMoreLikelyToBeExercised()
    {
        // A mutated character is one of ~100 alternatives of <character>, so it is rarely picked by chance.
        var mutation = new GrammarMutation(MutationOperator.ReplaceSymbol, "<character>", 0, 0, "\u0000");
        var mutant = mutation.Apply(Json);

        double HitRate(GeneratorOptions options)
        {
            var gen = new GrammarGenerator(mutant, options);
            return Enumerable.Range(0, 300).Count(s => mutation.IsExercisedBy(gen.GenerateTree(new Random(s)))) / 300.0;
        }

        var plain = HitRate(JsonGrammar.Options(3));
        var focused = HitRate(JsonGrammar.Options(3) with { Focus = mutation.MutatedAlternative });
        output.WriteLine($"exercised: uniform {plain:P0}, focused {focused:P0}");
        Assert.True(focused > plain);
    }

    /// <summary>
    /// Not a correctness test: a small end-to-end run of the experiment against System.Text.Json, to show the
    /// shape of the results. For each mutant we generate up to <c>MaxAttempts</c> inputs and keep the first
    /// <c>InputsPerMutant</c> that actually exercise the mutation; only those count. Outcome per input:
    /// accepted, rejected (JsonException) or crash (any other exception).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MutationExperimentAgainstSystemTextJson(bool focused)
    {
        const int inputsPerMutant = 5;
        const int maxAttempts = 20;
        var sw = Stopwatch.StartNew();
        var rows = new List<Row>();

        foreach (var (mutation, grammar) in GrammarMutator.Mutants(Json, JsonMutation))
        {
            var options = JsonGrammar.Options(3);
            if (focused) options = options with { Focus = mutation.MutatedAlternative };
            var gen = new GrammarGenerator(grammar, options);

            var row = new Row(mutation);
            for (int seed = 0; seed < maxAttempts && row.Exercised < inputsPerMutant; seed++)
            {
                var tree = gen.GenerateTree(new Random(seed));
                row.Generated++;
                if (!mutation.IsExercisedBy(tree)) continue;

                row.Exercised++;
                try
                {
                    using var _ = JsonDocument.Parse(tree.ToString());
                    row.Accepted++;
                }
                catch (JsonException)
                {
                    row.Rejected++;
                }
                catch (Exception e)
                {
                    row.Crashed++;
                    row.Crash ??= $"{e.GetType().Name}: {e.Message}";
                }
            }

            rows.Add(row);
        }

        output.WriteLine($"{(focused ? "Focused" : "Uniform")} generation: {rows.Count} mutants, " +
                         $"up to {inputsPerMutant} exercising inputs out of {maxAttempts} attempts, {sw.ElapsedMilliseconds} ms\n");
        output.WriteLine($"{"operator",-18} {"mutants",8} {"never hit",10} {"hit rate",9} " +
                         $"{"all accepted",13} {"some rejected",14} {"crashing",9}");
        foreach (var g in rows.GroupBy(r => r.Mutation.Operator))
        {
            var hit = g.Where(r => r.Exercised > 0).ToList();
            double hitRate = g.Sum(r => r.Exercised) / (double)g.Sum(r => r.Generated);
            output.WriteLine($"{g.Key,-18} {g.Count(),8} {g.Count() - hit.Count,10} {hitRate,9:P0} " +
                             $"{hit.Count(r => r.Accepted == r.Exercised),13} {hit.Count(r => r.Rejected > 0),14} " +
                             $"{hit.Count(r => r.Crashed > 0),9}");
        }

        output.WriteLine("\n'all accepted' and the outcome columns only count mutants that were hit at least once.");
        output.WriteLine("\nMutants whose inputs throw something other than JsonException (first 10):");
        foreach (var r in rows.Where(r => r.Crashed > 0).Take(10))
            output.WriteLine($"  {r.Mutation.Describe(Json)}\n      {r.Crash}");
    }

    private sealed class Row(GrammarMutation mutation)
    {
        public GrammarMutation Mutation => mutation;
        public int Generated, Exercised, Accepted, Rejected, Crashed;
        public string? Crash;
    }
}
