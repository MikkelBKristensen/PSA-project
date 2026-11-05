using Fuzzers.Grammar;

namespace Fuzzers.Harness;

/// <summary>A fuzzing strategy: produces the next input to test, and where it came from.</summary>
public interface IInputSource
{
    string Name { get; }

    /// <summary>The next input, or <c>null</c> if this attempt produced nothing worth running.</summary>
    (string Input, string Origin)? Next(Random rand);
}

/// <summary>Arm A: valid JSON from the original grammar.</summary>
public sealed class GrammarSource(int maxDepth) : IInputSource
{
    private readonly GrammarGenerator generator = new(JsonGrammar.Create(), JsonGrammar.Options(maxDepth));

    public string Name => "grammar";

    public (string Input, string Origin)? Next(Random rand) => (generator.GenerateTree(rand).ToString(), "grammar");
}

/// <summary>
/// Arms D/E: pick a random first-order grammar mutant and generate from it. Inputs that do not exercise the
/// mutation are skipped (they are just valid JSON).
/// </summary>
public sealed class GrammarMutationSource : IInputSource
{
    private const int MaxAttempts = 20;

    private static readonly ContextFreeGrammar Json = JsonGrammar.Create();
    private readonly (GrammarMutation Mutation, ContextFreeGrammar Grammar)[] mutants;
    private readonly GrammarGenerator?[] generators;
    private readonly string?[] descriptions;
    private readonly int maxDepth;
    private readonly bool focused;

    public GrammarMutationSource(int maxDepth, bool focused)
    {
        this.maxDepth = maxDepth;
        this.focused = focused;
        mutants = GrammarMutator.Mutants(Json, new MutatorOptions { Terminals = JsonGrammar.MutationTerminals }).ToArray();
        generators = new GrammarGenerator?[mutants.Length];
        descriptions = new string?[mutants.Length];
    }

    public string Name => focused ? "grammar-mutation-focused" : "grammar-mutation";

    public int MutantCount => mutants.Length;

    public (string Input, string Origin)? Next(Random rand)
    {
        int i = rand.Next(mutants.Length);
        var (mutation, _) = mutants[i];
        var gen = generators[i] ??= Create(i);

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var tree = gen.GenerateTree(rand);
            if (mutation.IsExercisedBy(tree))
                return (tree.ToString(), descriptions[i] ??= Findings.Escape(mutation.Describe(Json)));
        }

        return null;
    }

    private GrammarGenerator Create(int i)
    {
        var options = JsonGrammar.Options(maxDepth);
        if (focused) options = options with { Focus = mutants[i].Mutation.MutatedAlternative };
        return new GrammarGenerator(mutants[i].Grammar, options);
    }
}

public static class InputSources
{
    public static readonly string[] Names = ["grammar", "grammar-mutation", "grammar-mutation-focused"];

    public static IInputSource Create(string name, int maxDepth) => name switch
    {
        "grammar" => new GrammarSource(maxDepth),
        "grammar-mutation" => new GrammarMutationSource(maxDepth, focused: false),
        "grammar-mutation-focused" => new GrammarMutationSource(maxDepth, focused: true),
        _ => throw new ArgumentException($"unknown strategy '{name}' (expected one of: {string.Join(", ", Names)})"),
    };
}
