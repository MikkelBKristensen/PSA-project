using Fuzzers.Grammar;
using Fuzzers.Guidance;
using Fuzzers.InputMutation;

namespace Fuzzers.Harness;

/// <summary>A fuzzing strategy: produces the next input to test, and where it came from.</summary>
public interface IInputSource
{
    string Name { get; }

    /// <summary>The next input, or <c>null</c> if this attempt produced nothing worth running.</summary>
    (string Input, string Origin)? Next(Random rand);

    /// <summary>Called with the parsers' outcomes for the input last returned by <see cref="Next"/>.</summary>
    void Feedback(IReadOnlyList<IJsonParser> parsers, IReadOnlyList<Outcome> outcomes) { }

    /// <summary>Strategy-specific progress numbers (e.g. corpus size), or <c>null</c>.</summary>
    IReadOnlyDictionary<string, long>? Metrics => null;
}

/// <summary>Arm A: valid JSON from the original grammar.</summary>
public sealed class GrammarSource(int maxDepth) : IInputSource
{
    private readonly GrammarGenerator generator = new(JsonGrammar.Create(), JsonGrammar.Options(maxDepth));

    public string Name => "grammar";

    public (string Input, string Origin)? Next(Random rand) => (generator.GenerateTree(rand).ToString(), "grammar");
}

/// <summary>
/// Grammar mutation: pick a random first-order grammar mutant and generate from it. Inputs that do not exercise
/// the mutation are skipped (they are just valid JSON).
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

/// <summary>
/// Input mutation: take an input, mutate it, test it. Unguided, every input starts from a fresh grammar-generated
/// one. Guided, inputs are taken from a corpus, and a mutant joins the corpus if it shows a new feature
/// (k-paths of its derivation tree and/or a new parser outcome). Covers four arms:
/// char/tree level × unguided/guided.
/// </summary>
public sealed class InputMutationSource : IInputSource
{
    /// <summary>Share of guided inputs that are fresh grammar inputs instead of corpus mutants.</summary>
    private const double FreshRate = 0.05;
    private const int SeedCount = 100;
    private const int K = 3;

    private readonly GrammarGenerator generator;
    private readonly CharMutator? charMutator;
    private readonly TreeMutator? treeMutator;
    private readonly Corpus? corpus;
    private readonly HashSet<int> features = new();
    private (string Input, DerivationTree? Tree)? last;

    public InputMutationSource(int maxDepth, bool treeLevel, bool guided)
    {
        var grammar = JsonGrammar.Create();
        generator = new GrammarGenerator(grammar, JsonGrammar.Options(maxDepth));
        if (treeLevel) treeMutator = new TreeMutator(grammar, generator, JsonGrammar.MutationTerminals);
        else charMutator = new CharMutator(JsonGrammar.MutationTerminals);
        if (guided) corpus = new Corpus();
        Name = (treeLevel ? "tree" : "char") + "-mutation" + (guided ? "-guided" : "");
    }

    public string Name { get; }

    private Random? rand;

    public (string Input, string Origin)? Next(Random rand)
    {
        this.rand = rand;
        bool fresh = corpus is null || corpus.Count < SeedCount || rand.NextDouble() < FreshRate;
        if (corpus is not null && fresh)
        {
            // Guided seeding: plain grammar inputs, added to the corpus if they are diverse.
            var seed = generator.GenerateTree(rand);
            var s = seed.ToString();
            last = (s, seed);
            return (s, "grammar seed");
        }

        DerivationTree tree;
        string input;
        if (corpus is null)
        {
            tree = generator.GenerateTree(rand);
            input = tree.ToString();
        }
        else
        {
            var parent = corpus.Pick(rand);
            tree = parent.Tree!;
            input = parent.Input;
        }

        if (treeMutator is not null)
        {
            var (mutated, description) = treeMutator.Mutate(tree, rand, corpus is null ? null : sym => corpus.Donor(sym, rand));
            var s = mutated.ToString();
            last = (s, mutated);
            return (s, description);
        }
        else
        {
            var (mutated, description) = charMutator!.Mutate(input, rand);
            // Char mutants have no derivation tree; to keep mutating them, the corpus stores the string and the
            // tree of the input they came from is dropped (see Feedback).
            last = (mutated, null);
            return (mutated, description);
        }
    }

    public void Feedback(IReadOnlyList<IJsonParser> parsers, IReadOnlyList<Outcome> outcomes)
    {
        if (corpus is null || last is not { } l) return;

        features.Clear();
        if (l.Tree is not null) Features.KPaths(l.Tree, K, features);
        Features.Outcomes(parsers, outcomes, features);
        corpus.TryAdd(l.Input, l.Tree, features, rand!);
    }

    public IReadOnlyDictionary<string, long>? Metrics => corpus is null
        ? null
        : new Dictionary<string, long> { ["corpus"] = corpus.Count, ["features"] = corpus.FeatureCount };
}

public static class InputSources
{
    public static readonly string[] Names =
    [
        "grammar", "grammar-mutation", "grammar-mutation-focused",
        "char-mutation", "char-mutation-guided", "tree-mutation", "tree-mutation-guided",
    ];

    public static IInputSource Create(string name, int maxDepth) => name switch
    {
        "grammar" => new GrammarSource(maxDepth),
        "grammar-mutation" => new GrammarMutationSource(maxDepth, focused: false),
        "grammar-mutation-focused" => new GrammarMutationSource(maxDepth, focused: true),
        "char-mutation" => new InputMutationSource(maxDepth, treeLevel: false, guided: false),
        "char-mutation-guided" => new InputMutationSource(maxDepth, treeLevel: false, guided: true),
        "tree-mutation" => new InputMutationSource(maxDepth, treeLevel: true, guided: false),
        "tree-mutation-guided" => new InputMutationSource(maxDepth, treeLevel: true, guided: true),
        _ => throw new ArgumentException($"unknown strategy '{name}' (expected one of: {string.Join(", ", Names)})"),
    };
}
