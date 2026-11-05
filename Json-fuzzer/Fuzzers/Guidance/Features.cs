using Fuzzers.Harness;

namespace Fuzzers.Guidance;

/// <summary>
/// Feedback signals for guided fuzzing. An input is "interesting" if it has a feature no earlier input had.
/// Features are process-local hashes (string hashing is randomised per process), so they are only compared
/// within one fuzzing process.
/// </summary>
public static class Features
{
    private const int KPathTag = 0x4B50;   // "KP"
    private const int OutcomeTag = 0x4F43; // "OC"

    /// <summary>
    /// Structural diversity: every path of 1..<paramref name="k"/> consecutive nodes (symbol + chosen
    /// alternative) in the derivation tree. Follows Havrikov &amp; Zeller, "Systematically Covering Input
    /// Structure" (ASE 2019). Mutated nodes carry alternative <see cref="InputMutation.TreeMutator.Mutated"/>,
    /// so structure the grammar cannot produce shows up as new features.
    /// </summary>
    public static void KPaths(DerivationTree tree, int k, HashSet<int> into)
    {
        var path = new List<int>(); // path[d] = hash of the ancestor at depth d on the current root-to-node path
        Visit(tree, 0);

        void Visit(DerivationTree node, int depth)
        {
            var nodeHash = HashCode.Combine(node.Symbol, node.Alternative);
            if (depth == path.Count) path.Add(nodeHash);
            else path[depth] = nodeHash;

            int h = KPathTag;
            for (int len = 1; len <= Math.Min(k, depth + 1); len++)
            {
                h = HashCode.Combine(h, path[depth - len + 1]);
                into.Add(h);
            }

            foreach (var child in node.Children) Visit(child, depth + 1);
        }
    }

    /// <summary>
    /// Behavioural diversity: per parser, its verdict and normalised error (type, message, crash location),
    /// plus the combination of verdicts across parsers. A new error message roughly means a new error path.
    /// </summary>
    public static void Outcomes(IReadOnlyList<IJsonParser> parsers, IReadOnlyList<Outcome> outcomes, HashSet<int> into)
    {
        int combined = OutcomeTag;
        for (int i = 0; i < parsers.Count; i++)
        {
            var o = outcomes[i];
            combined = HashCode.Combine(combined, o.Verdict);
            if (o.Verdict == Verdict.Accepted) continue;
            into.Add(HashCode.Combine(OutcomeTag, i, o.Verdict, o.ErrorType, Findings.NormalizeMessage(o.Message), o.Location));
        }

        into.Add(combined);
    }
}
