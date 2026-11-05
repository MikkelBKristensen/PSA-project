using System.Text;

namespace Fuzzers.Grammar;

/// <summary>
/// A context-free grammar stored as data: nonterminal -> alternatives -> sequence of symbols.
/// </summary>
/// <remarks>
/// A symbol written as <c>&lt;name&gt;</c> is a nonterminal; anything else is a terminal (emitted literally).
/// An empty alternative is epsilon. Alternative arrays are treated as immutable: mutations replace them
/// instead of editing them in place, so <see cref="Clone"/> only needs to copy the lists.
/// </remarks>
public sealed class ContextFreeGrammar(string start = "<start>")
{
    private readonly Dictionary<string, List<string[]>> rules = new();
    private readonly List<string> order = new();

    public string Start { get; } = start;

    /// <summary>Nonterminals in definition order.</summary>
    public IReadOnlyList<string> NonTerminals => order;

    public List<string[]> this[string nonTerminal]
    {
        get => rules[nonTerminal];
        set
        {
            if (!IsNonTerminal(nonTerminal))
                throw new ArgumentException($"'{nonTerminal}' is not a nonterminal (expected <name>).");
            if (rules.TryAdd(nonTerminal, value)) order.Add(nonTerminal);
            else rules[nonTerminal] = value;
        }
    }

    public bool Defines(string nonTerminal) => rules.ContainsKey(nonTerminal);

    public static bool IsNonTerminal(string symbol) =>
        symbol.Length > 2 && symbol[0] == '<' && symbol[^1] == '>';

    /// <summary>All distinct terminals that occur in the grammar.</summary>
    public IEnumerable<string> Terminals() =>
        order.SelectMany(nt => rules[nt]).SelectMany(alt => alt).Where(s => !IsNonTerminal(s)).Distinct();

    public ContextFreeGrammar Clone()
    {
        var copy = new ContextFreeGrammar(Start);
        foreach (var nt in order) copy[nt] = new List<string[]>(rules[nt]);
        return copy;
    }

    public string FormatRule(string nonTerminal) =>
        $"{nonTerminal} ::= {string.Join(" | ", rules[nonTerminal].Select(FormatAlternative))}";

    public static string FormatAlternative(string[] alternative) =>
        alternative.Length == 0 ? "ε" : string.Join(" ", alternative.Select(FormatSymbol));

    public static string FormatSymbol(string symbol)
    {
        if (IsNonTerminal(symbol)) return symbol;

        var sb = new StringBuilder("'");
        foreach (var c in symbol)
        {
            switch (c)
            {
                case '\'': sb.Append("\\'"); break;
                case '\\': sb.Append("\\\\"); break;
                default:
                    if (char.IsControl(c) || char.IsSurrogate(c) || char.IsWhiteSpace(c) && c != ' ')
                        sb.Append($"\\u{(int)c:X4}");
                    else
                        sb.Append(c);
                    break;
            }
        }

        return sb.Append('\'').ToString();
    }

    public override string ToString() => string.Join(Environment.NewLine, order.Select(FormatRule));
}
