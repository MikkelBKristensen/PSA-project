using Fuzzers.Grammar;

namespace Fuzzers;

public enum MutationOperator
{
    /// <summary>Remove one symbol from an alternative.     <c>&lt;member&gt; ::= &lt;string&gt; ':' &lt;element&gt;</c> → drop <c>':'</c></summary>
    DeleteSymbol,

    /// <summary>Repeat one symbol in an alternative.       <c>',' → ',' ','</c></summary>
    DuplicateSymbol,

    /// <summary>Swap two adjacent symbols.                 <c>&lt;string&gt; ':' → ':' &lt;string&gt;</c></summary>
    SwapAdjacent,

    /// <summary>Replace a symbol by another nonterminal or terminal. <c>'}' → ']'</c>, <c>&lt;string&gt; → &lt;value&gt;</c></summary>
    ReplaceSymbol,

    /// <summary>Remove a whole alternative (e.g. the empty-array base case).</summary>
    DeleteAlternative,

    /// <summary>Add a single-terminal alternative. <c>&lt;value&gt; ::= ... | 'NaN'</c></summary>
    AddAlternative,
}

/// <summary>A first-order mutation: one edit to one rule of a grammar.</summary>
public sealed record GrammarMutation(
    MutationOperator Operator,
    string Rule,
    int Alternative,
    int Position = -1,
    string? Symbol = null)
{
    /// <summary>
    /// The (rule, alternative index) in the <em>mutant</em> grammar that carries the change, or <c>null</c> for
    /// <see cref="MutationOperator.DeleteAlternative"/>, which removes an alternative instead of changing one.
    /// </summary>
    public (string Rule, int Alternative)? MutatedAlternative =>
        Operator == MutationOperator.DeleteAlternative ? null : (Rule, Alternative);

    /// <summary>
    /// True if a derivation from the mutant grammar actually went through the mutation. An input that did not
    /// is just an input of the original grammar, so its outcome says nothing about the mutation.
    /// For DeleteAlternative this means the rule was expanded at all (the removed choice was not available).
    /// </summary>
    public bool IsExercisedBy(DerivationTree tree) => MutatedAlternative is { } m
        ? tree.Descendants().Any(n => n.Symbol == m.Rule && n.Alternative == m.Alternative)
        : tree.Contains(Rule);

    public ContextFreeGrammar Apply(ContextFreeGrammar grammar)
    {
        var result = grammar.Clone();
        var alts = result[Rule];

        switch (Operator)
        {
            case MutationOperator.DeleteSymbol:
                alts[Alternative] = [.. alts[Alternative][..Position], .. alts[Alternative][(Position + 1)..]];
                break;
            case MutationOperator.DuplicateSymbol:
                alts[Alternative] = [.. alts[Alternative][..(Position + 1)], .. alts[Alternative][Position..]];
                break;
            case MutationOperator.SwapAdjacent:
            {
                var alt = alts[Alternative].ToArray();
                (alt[Position], alt[Position + 1]) = (alt[Position + 1], alt[Position]);
                alts[Alternative] = alt;
                break;
            }
            case MutationOperator.ReplaceSymbol:
            {
                var alt = alts[Alternative].ToArray();
                alt[Position] = Symbol!;
                alts[Alternative] = alt;
                break;
            }
            case MutationOperator.DeleteAlternative:
                alts.RemoveAt(Alternative);
                break;
            case MutationOperator.AddAlternative:
                alts.Add([Symbol!]);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return result;
    }

    /// <summary>Human-readable before → after, e.g. for result tables.</summary>
    public string Describe(ContextFreeGrammar original)
    {
        var alts = original[Rule];
        return Operator switch
        {
            MutationOperator.DeleteAlternative =>
                $"{Operator}: {Rule} ::= ... | {ContextFreeGrammar.FormatAlternative(alts[Alternative])}  ⇒  removed",
            MutationOperator.AddAlternative =>
                $"{Operator}: {Rule} ::= ... | {ContextFreeGrammar.FormatSymbol(Symbol!)}",
            _ =>
                $"{Operator}: {Rule} ::= {ContextFreeGrammar.FormatAlternative(alts[Alternative])}  ⇒  " +
                ContextFreeGrammar.FormatAlternative(Apply(original)[Rule][Alternative]),
        };
    }
}

public sealed record MutatorOptions
{
    /// <summary>Rules to mutate; <c>null</c> means all.</summary>
    public IReadOnlySet<string>? Rules { get; init; }

    /// <summary>Operators to apply; <c>null</c> means all.</summary>
    public IReadOnlySet<MutationOperator>? Operators { get; init; }

    /// <summary>
    /// Terminals used by <see cref="MutationOperator.ReplaceSymbol"/> and <see cref="MutationOperator.AddAlternative"/>.
    /// <c>null</c> means every terminal already in the grammar. (Nonterminals are always used by ReplaceSymbol.)
    /// </summary>
    public IReadOnlyList<string>? Terminals { get; init; }
}

/// <summary>Systematically enumerates first-order mutants of a grammar.</summary>
public static class GrammarMutator
{
    /// <summary>
    /// Every distinct first-order mutant. Mutants that leave the rule unchanged, duplicate an earlier
    /// mutant, or can no longer derive any finite string are skipped.
    /// </summary>
    public static IEnumerable<(GrammarMutation Mutation, ContextFreeGrammar Grammar)> Mutants(
        ContextFreeGrammar grammar, MutatorOptions? options = null)
    {
        var seen = new HashSet<string>();
        foreach (var mutation in Mutations(grammar, options ?? new MutatorOptions()))
        {
            var mutant = mutation.Apply(grammar);
            var rule = mutant.FormatRule(mutation.Rule);
            if (rule == grammar.FormatRule(mutation.Rule)) continue;
            if (!seen.Add(rule)) continue;
            if (!GrammarGenerator.IsProductive(mutant)) continue;
            yield return (mutation, mutant);
        }
    }

    /// <summary>Raw candidate mutations, before filtering out equivalent or unproductive ones.</summary>
    public static IEnumerable<GrammarMutation> Mutations(ContextFreeGrammar grammar, MutatorOptions options)
    {
        var terminals = options.Terminals ?? grammar.Terminals().ToList();
        var replacements = grammar.NonTerminals.Concat(terminals).Distinct().ToList();
        bool Use(MutationOperator op) => options.Operators?.Contains(op) ?? true;

        foreach (var rule in grammar.NonTerminals)
        {
            if (options.Rules is not null && !options.Rules.Contains(rule)) continue;
            var alts = grammar[rule];

            for (int a = 0; a < alts.Count; a++)
            {
                var alt = alts[a];
                for (int p = 0; p < alt.Length; p++)
                {
                    if (Use(MutationOperator.DeleteSymbol))
                        yield return new(MutationOperator.DeleteSymbol, rule, a, p);
                    if (Use(MutationOperator.DuplicateSymbol))
                        yield return new(MutationOperator.DuplicateSymbol, rule, a, p);
                    if (Use(MutationOperator.SwapAdjacent) && p + 1 < alt.Length)
                        yield return new(MutationOperator.SwapAdjacent, rule, a, p);
                    if (Use(MutationOperator.ReplaceSymbol))
                        foreach (var r in replacements.Where(r => r != alt[p]))
                            yield return new(MutationOperator.ReplaceSymbol, rule, a, p, r);
                }

                if (Use(MutationOperator.DeleteAlternative) && alts.Count > 1)
                    yield return new(MutationOperator.DeleteAlternative, rule, a);
            }

            if (Use(MutationOperator.AddAlternative))
                foreach (var t in terminals.Where(t => !alts.Any(alt => alt.Length == 1 && alt[0] == t)))
                    yield return new(MutationOperator.AddAlternative, rule, alts.Count, Symbol: t);
        }
    }
}
