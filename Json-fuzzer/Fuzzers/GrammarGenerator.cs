using System.Text;
using Fuzzers.Grammar;

namespace Fuzzers;

public sealed record GeneratorOptions
{
    /// <summary>
    /// Hard limit on depth, counted in <see cref="DepthSymbols"/> along any root-to-leaf path.
    /// </summary>
    public int MaxDepth { get; init; } = int.MaxValue;

    /// <summary>Nonterminals that count towards <see cref="MaxDepth"/>; <c>null</c> means every nonterminal.</summary>
    public IReadOnlySet<string>? DepthSymbols { get; init; }

    /// <summary>
    /// Soft limit on derivation tree depth. Keeps rules that are not depth symbols (strings, digits, whitespace)
    /// from recursing forever. Only exceeded when the grammar has no shorter derivation.
    /// </summary>
    public int MaxTreeDepth { get; init; } = 200;

    /// <summary>
    /// Soft limit on the number of expanded nonterminals. Once reached, every remaining nonterminal takes its
    /// cheapest alternative so generation finishes quickly. This stops mutated grammars from blowing up
    /// (e.g. <c>&lt;a&gt; ::= 'x' | &lt;a&gt; &lt;a&gt; &lt;a&gt;</c>).
    /// </summary>
    public int MaxNodes { get; init; } = 5_000;

    /// <summary>
    /// Optional (rule, alternative) to steer towards, typically the alternative a mutation changed
    /// (<see cref="GrammarMutation.MutatedAlternative"/>). Whenever <c>Rule</c> is expanded and the alternative
    /// fits the budget, it is chosen with probability <see cref="FocusProbability"/> instead of uniformly.
    /// This only biases the choice at the rule itself; whether the rule is reached at all is still random.
    /// </summary>
    public (string Rule, int Alternative)? Focus { get; init; }

    public double FocusProbability { get; init; } = 0.5;
}

/// <summary>A derivation tree; its string is the concatenation of its terminal leaves.</summary>
public sealed class DerivationTree(string symbol, int alternative, IReadOnlyList<DerivationTree> children)
{
    public string Symbol => symbol;

    /// <summary>Index of the alternative chosen for this nonterminal; -1 for terminals.</summary>
    public int Alternative => alternative;

    public IReadOnlyList<DerivationTree> Children => children;
    public bool IsTerminal => !ContextFreeGrammar.IsNonTerminal(symbol);

    /// <summary>This node and all nodes below it, in pre-order.</summary>
    public IEnumerable<DerivationTree> Descendants()
    {
        var stack = new Stack<DerivationTree>();
        stack.Push(this);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;
            for (int i = node.Children.Count - 1; i >= 0; i--) stack.Push(node.Children[i]);
        }
    }

    public bool Contains(string sym) => Descendants().Any(n => n.Symbol == sym);

    public override string ToString()
    {
        var sb = new StringBuilder();
        Append(sb);
        return sb.ToString();
    }

    private void Append(StringBuilder sb)
    {
        if (IsTerminal) sb.Append(symbol);
        else foreach (var c in children) c.Append(sb);
    }
}

/// <summary>
/// Generates random strings from any <see cref="ContextFreeGrammar"/>, with depth control.
/// </summary>
/// <remarks>
/// For every nonterminal we precompute the cheapest possible derivation, once in depth symbols and once in
/// tree depth. When expanding, an alternative is only chosen if the cheapest way to finish it still fits in
/// the remaining budget. That way the limit is always respected and generation never gets stuck.
/// Alternatives that cannot derive any finite string (possible in mutated grammars) are never chosen.
/// </remarks>
public sealed class GrammarGenerator
{
    private const int Infinite = int.MaxValue;

    private readonly ContextFreeGrammar grammar;
    private readonly GeneratorOptions options;
    private readonly Dictionary<string, (int Nest, int Tree)[]> altCosts = new();

    public GrammarGenerator(ContextFreeGrammar grammar, GeneratorOptions? options = null)
    {
        this.grammar = grammar;
        this.options = options ?? new GeneratorOptions();

        if (!grammar.Defines(grammar.Start))
            throw new ArgumentException($"start symbol {grammar.Start} is not defined");

        var minNest = MinCosts(grammar, NestWeight);
        var minTree = MinCosts(grammar, _ => 1);

        if (minTree[grammar.Start] == Infinite)
            throw new ArgumentException("the grammar cannot derive any finite string");
        if (minNest[grammar.Start] > this.options.MaxDepth)
            throw new ArgumentException(
                $"MaxDepth {this.options.MaxDepth} is below the minimum depth {minNest[grammar.Start]} of the grammar");

        foreach (var nt in grammar.NonTerminals)
            altCosts[nt] = grammar[nt].Select(alt => (AltCost(alt, minNest), AltCost(alt, minTree))).ToArray();
    }

    /// <summary>True if the start symbol can derive at least one finite string.</summary>
    public static bool IsProductive(ContextFreeGrammar grammar) =>
        grammar.Defines(grammar.Start) && MinCosts(grammar, _ => 1)[grammar.Start] != Infinite;

    public DerivationTree GenerateTree(Random rand) =>
        new Run(this, rand).Expand(grammar.Start, NestWeight(grammar.Start), 1);

    public string Generate(int? seed = null) =>
        GenerateTree(seed is null ? new Random() : new Random(seed.Value)).ToString();

    private int NestWeight(string nonTerminal) =>
        options.DepthSymbols is null || options.DepthSymbols.Contains(nonTerminal) ? 1 : 0;

    /// <summary>
    /// Least-fixed-point computation of the cheapest derivation of each nonterminal, where a derivation costs
    /// the maximum, over root-to-leaf paths, of the summed weights. Infinite = no finite derivation exists.
    /// </summary>
    private static Dictionary<string, int> MinCosts(ContextFreeGrammar grammar, Func<string, int> weight)
    {
        var cost = grammar.NonTerminals.ToDictionary(nt => nt, _ => Infinite);
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var nt in grammar.NonTerminals)
            {
                foreach (var alt in grammar[nt])
                {
                    int c = AltCost(alt, cost);
                    if (c == Infinite) continue;
                    c += weight(nt);
                    if (c < cost[nt])
                    {
                        cost[nt] = c;
                        changed = true;
                    }
                }
            }
        }

        return cost;
    }

    /// <summary>Cost of finishing an alternative: the most expensive nonterminal in it (terminals are free).</summary>
    private static int AltCost(string[] alt, IReadOnlyDictionary<string, int> cost)
    {
        int max = 0;
        foreach (var s in alt)
        {
            if (!ContextFreeGrammar.IsNonTerminal(s)) continue;
            // Undefined nonterminals (e.g. introduced by a mutation) make the alternative unusable.
            if (!cost.TryGetValue(s, out var c) || c == Infinite) return Infinite;
            max = Math.Max(max, c);
        }

        return max;
    }

    private sealed class Run(GrammarGenerator gen, Random rand)
    {
        private int nodes;

        // nestUsed: depth symbols on the path so far, including `symbol` itself.
        public DerivationTree Expand(string symbol, int nestUsed, int treeDepth)
        {
            if (!ContextFreeGrammar.IsNonTerminal(symbol)) return new DerivationTree(symbol, -1, []);

            nodes++;
            int index = Choose(symbol, nestUsed, treeDepth);
            var alt = gen.grammar[symbol][index];
            var children = new DerivationTree[alt.Length];
            for (int i = 0; i < alt.Length; i++)
            {
                var s = alt[i];
                int childNest = ContextFreeGrammar.IsNonTerminal(s) ? nestUsed + gen.NestWeight(s) : nestUsed;
                children[i] = Expand(s, childNest, treeDepth + 1);
            }

            return new DerivationTree(symbol, index, children);
        }

        // Reused across calls: Choose runs once per node, so it must not allocate.
        private readonly List<int> candidates = new();
        private readonly List<int> picks = new();

        private int Choose(string symbol, int nestUsed, int treeDepth)
        {
            var costs = gen.altCosts[symbol];
            var o = gen.options;

            // Hard limit: the cheapest way to finish must fit in the nesting budget. Non-empty by construction:
            // the parent only picked an alternative containing `symbol` because `symbol` could finish in budget.
            candidates.Clear();
            for (int i = 0; i < costs.Length; i++)
                if (costs[i].Nest != Infinite && nestUsed + costs[i].Nest <= o.MaxDepth) candidates.Add(i);

            if (nodes <= o.MaxNodes)
            {
                picks.Clear();
                foreach (var i in candidates)
                    if (treeDepth + costs[i].Tree <= o.MaxTreeDepth) picks.Add(i);

                if (picks.Count > 0)
                {
                    if (o.Focus is { } focus && focus.Rule == symbol && picks.Contains(focus.Alternative)
                        && rand.NextDouble() < o.FocusProbability)
                        return focus.Alternative;
                    return picks[rand.Next(picks.Count)];
                }
            }

            // Out of budget: take the shortest way out.
            int min = int.MaxValue;
            foreach (var i in candidates) min = Math.Min(min, costs[i].Tree);
            picks.Clear();
            foreach (var i in candidates)
                if (costs[i].Tree == min) picks.Add(i);
            return picks[rand.Next(picks.Count)];
        }
    }
}
