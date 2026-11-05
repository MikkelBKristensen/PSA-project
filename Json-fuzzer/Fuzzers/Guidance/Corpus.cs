namespace Fuzzers.Guidance;

/// <summary>
/// The population of a guided fuzzer: inputs that each added at least one new feature when they were found.
/// </summary>
/// <remarks>
/// Parent selection is a size-4 tournament on (1 + new features at discovery) / (1 + times chosen): entries
/// that brought a lot of novelty are mutated more, but every entry's weight decays as it is used. Tournaments
/// keep selection O(1) regardless of corpus size.
/// </remarks>
public sealed class Corpus(int capacity = 20_000)
{
    private const int Tournament = 4;
    private const int MaxSubtreesPerSymbol = 10_000;

    public sealed class Entry(string input, DerivationTree? tree, int newFeatures)
    {
        public string Input => input;
        public DerivationTree? Tree => tree;
        public int NewFeatures => newFeatures;
        public int Chosen { get; set; }
        public double Weight => (1.0 + newFeatures) / (1.0 + Chosen);
    }

    private readonly List<Entry> entries = new();
    private readonly HashSet<int> seen = new();
    private readonly Dictionary<string, List<DerivationTree>> subtrees = new();

    public int Count => entries.Count;
    public int FeatureCount => seen.Count;

    /// <summary>Adds the input if it has a feature not seen before. Returns whether it was added.</summary>
    public bool TryAdd(string input, DerivationTree? tree, HashSet<int> features, Random rand)
    {
        int fresh = 0;
        foreach (var f in features)
            if (seen.Add(f)) fresh++;
        if (fresh == 0) return false;

        var entry = new Entry(input, tree, fresh);
        if (entries.Count < capacity) entries.Add(entry);
        else entries[Weakest(rand)] = entry;

        if (tree is not null) IndexSubtrees(tree, rand);
        return true;
    }

    public Entry Pick(Random rand)
    {
        var best = entries[rand.Next(entries.Count)];
        for (int i = 1; i < Tournament; i++)
        {
            var e = entries[rand.Next(entries.Count)];
            if (e.Weight > best.Weight) best = e;
        }

        best.Chosen++;
        return best;
    }

    /// <summary>A random subtree for <paramref name="symbol"/> from any corpus input, for splicing.</summary>
    public DerivationTree? Donor(string symbol, Random rand) =>
        subtrees.TryGetValue(symbol, out var list) && list.Count > 0 ? list[rand.Next(list.Count)] : null;

    private int Weakest(Random rand)
    {
        int worst = rand.Next(entries.Count);
        for (int i = 1; i < Tournament; i++)
        {
            int j = rand.Next(entries.Count);
            if (entries[j].Weight < entries[worst].Weight) worst = j;
        }

        return worst;
    }

    private void IndexSubtrees(DerivationTree tree, Random rand)
    {
        foreach (var node in tree.Descendants())
        {
            if (node.IsTerminal) continue;
            if (!subtrees.TryGetValue(node.Symbol, out var list)) subtrees[node.Symbol] = list = new();
            if (list.Count < MaxSubtreesPerSymbol) list.Add(node);
            else list[rand.Next(list.Count)] = node;
        }
    }
}
