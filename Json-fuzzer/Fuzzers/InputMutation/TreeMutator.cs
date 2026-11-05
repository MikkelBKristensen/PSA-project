using Fuzzers.Grammar;
using Fuzzers.Harness;

namespace Fuzzers.InputMutation;

/// <summary>
/// Grammar-aware mutation of a single input: edits its derivation tree. The operators mirror the grammar
/// mutation operators (delete / duplicate / swap / replace), but apply to one input instead of the language.
/// </summary>
public sealed class TreeMutator(ContextFreeGrammar grammar, GrammarGenerator generator, IReadOnlyList<string> tokens)
{
    /// <summary>Alternative index of a node whose children no longer match any grammar alternative.</summary>
    public const int Mutated = -2;

    public enum Op
    {
        /// <summary>Remove a subtree.</summary>
        Delete,

        /// <summary>Repeat a subtree next to itself.</summary>
        Duplicate,

        /// <summary>Swap a subtree with its neighbour.</summary>
        Swap,

        /// <summary>Replace a subtree with a fresh derivation of the same nonterminal (stays valid).</summary>
        Regenerate,

        /// <summary>Replace a subtree with one from another input with the same nonterminal (stays valid).</summary>
        Splice,

        /// <summary>Replace a subtree with a derivation of a different nonterminal.</summary>
        ReplaceNonTerminal,

        /// <summary>Replace a subtree with a token.</summary>
        ReplaceToken,

        /// <summary>Insert a token before a subtree.</summary>
        InsertToken,
    }

    private static readonly Op[] Ops = Enum.GetValues<Op>();

    /// <summary>Applies 1 to <paramref name="maxStack"/> random operations.</summary>
    /// <param name="donor">Returns a subtree for a nonterminal from elsewhere (e.g. the corpus), for Splice.</param>
    public (DerivationTree Tree, string Description) Mutate(
        DerivationTree tree, Random rand, Func<string, DerivationTree?>? donor = null, int maxStack = 4)
    {
        int n = rand.Next(1, maxStack + 1);
        var ops = new List<string>(n);
        for (int i = 0; i < n; i++)
        {
            (tree, var description) = MutateOnce(tree, Ops[rand.Next(Ops.Length)], rand, donor);
            ops.Add(description);
        }

        return (tree, "tree: " + string.Join(", ", ops));
    }

    public (DerivationTree Tree, string Description) MutateOnce(
        DerivationTree tree, Op op, Random rand, Func<string, DerivationTree?>? donor = null)
    {
        if (op is Op.Regenerate or Op.Splice)
        {
            // Replace a whole nonterminal subtree by another derivation of the same nonterminal: still valid.
            if (PickNode(tree, rand, n => !n.IsTerminal) is not { } target) return (tree, "none");
            var replacementTree = op == Op.Splice ? donor?.Invoke(target.Node.Symbol) : null;
            if (replacementTree is null) op = Op.Regenerate;
            replacementTree ??= generator.GenerateTree(rand, target.Node.Symbol);
            return (Rebuild(tree, target.Path, 0, replacementTree), $"{op} {target.Node.Symbol}");
        }

        // Structural edits: change the child list of a random node that has children.
        if (PickNode(tree, rand, n => n.Children.Count > 0) is not { } picked) return (tree, "none");
        var (parent, path) = picked;
        var kids = parent.Children.ToList();
        int c = rand.Next(kids.Count);
        var child = kids[c];
        int alt = Mutated;
        string? token = null;

        if (op == Op.Swap && kids.Count < 2) op = Op.Duplicate;

        switch (op)
        {
            case Op.Delete:
                kids.RemoveAt(c);
                break;
            case Op.Duplicate:
                kids.Insert(c, child);
                break;
            case Op.Swap:
                if (c == kids.Count - 1) c--;
                (kids[c], kids[c + 1]) = (kids[c + 1], kids[c]);
                break;
            case Op.ReplaceNonTerminal:
                var nt = grammar.NonTerminals[rand.Next(grammar.NonTerminals.Count)];
                kids[c] = generator.GenerateTree(rand, nt);
                break;
            case Op.ReplaceToken:
                token = tokens[rand.Next(tokens.Count)];
                kids[c] = new DerivationTree(token, -1, []);
                break;
            case Op.InsertToken:
                token = tokens[rand.Next(tokens.Count)];
                kids.Insert(c, new DerivationTree(token, -1, []));
                break;
        }

        var replacement = new DerivationTree(parent.Symbol, alt, kids);
        var description = $"{op} {parent.Symbol}[{c}]" + (token is null ? "" : $" '{Findings.Escape(token)}'");
        return (Rebuild(tree, path, 0, replacement), description);
    }

    /// <summary>Uniformly random node matching <paramref name="filter"/>, with its path from the root (reservoir sampling).</summary>
    private static (DerivationTree Node, int[] Path)? PickNode(DerivationTree root, Random rand, Func<DerivationTree, bool> filter)
    {
        (DerivationTree, int[])? picked = null;
        int seen = 0;
        var path = new List<int>();

        void Visit(DerivationTree node)
        {
            if (filter(node) && rand.Next(++seen) == 0) picked = (node, path.ToArray());
            for (int i = 0; i < node.Children.Count; i++)
            {
                path.Add(i);
                Visit(node.Children[i]);
                path.RemoveAt(path.Count - 1);
            }
        }

        Visit(root);
        return picked;
    }

    /// <summary>Copies the nodes along <paramref name="path"/>, putting <paramref name="replacement"/> at the end.</summary>
    private static DerivationTree Rebuild(DerivationTree node, int[] path, int depth, DerivationTree replacement)
    {
        if (depth == path.Length) return replacement;
        var kids = node.Children.ToArray();
        kids[path[depth]] = Rebuild(kids[path[depth]], path, depth + 1, replacement);
        return new DerivationTree(node.Symbol, node.Alternative, kids);
    }
}
