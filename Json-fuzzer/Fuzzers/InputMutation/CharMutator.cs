namespace Fuzzers.InputMutation;

/// <summary>
/// Classic, structure-unaware mutation fuzzing on the input string: the baseline the grammar-aware
/// approaches are compared against. Works on UTF-16 characters rather than bytes, because the parsers under
/// test take .NET strings; splitting a surrogate pair is allowed and produces lone surrogates.
/// </summary>
public sealed class CharMutator(IReadOnlyList<string> tokens)
{
    public enum Op { ReplaceChar, InsertChar, DeleteChar, DeleteRange, DuplicateRange, InsertToken, ReplaceRangeWithToken }

    private static readonly Op[] Ops = Enum.GetValues<Op>();
    private const string JsonChars = "{}[],:\"\\ -+.eE0123456789tfnulr";

    /// <summary>Applies 1 to <paramref name="maxStack"/> random operations.</summary>
    public (string Input, string Description) Mutate(string input, Random rand, int maxStack = 4)
    {
        int n = rand.Next(1, maxStack + 1);
        var ops = new List<string>(n);
        for (int i = 0; i < n; i++)
        {
            var op = Ops[rand.Next(Ops.Length)];
            input = Apply(op, input, rand);
            ops.Add(op.ToString());
        }

        return (input, "char: " + string.Join(", ", ops));
    }

    public string Apply(Op op, string s, Random rand)
    {
        int pos = rand.Next(s.Length + 1); // insertion point; s[pos] valid only when pos < s.Length
        switch (op)
        {
            case Op.ReplaceChar when s.Length > 0:
                pos = rand.Next(s.Length);
                return string.Concat(s.AsSpan(0, pos), RandomChar(rand).ToString(), s.AsSpan(pos + 1));
            case Op.InsertChar:
                return s.Insert(pos, RandomChar(rand).ToString());
            case Op.DeleteChar when s.Length > 0:
                return s.Remove(rand.Next(s.Length), 1);
            case Op.DeleteRange when s.Length > 0:
            {
                int start = rand.Next(s.Length);
                return s.Remove(start, Math.Min(rand.Next(1, 9), s.Length - start));
            }
            case Op.DuplicateRange when s.Length > 0:
            {
                int start = rand.Next(s.Length);
                int len = Math.Min(rand.Next(1, 17), s.Length - start);
                return s.Insert(start, s.Substring(start, len));
            }
            case Op.InsertToken:
                return s.Insert(pos, tokens[rand.Next(tokens.Count)]);
            case Op.ReplaceRangeWithToken when s.Length > 0:
            {
                int start = rand.Next(s.Length);
                int len = Math.Min(rand.Next(1, 5), s.Length - start);
                return s.Remove(start, len).Insert(start, tokens[rand.Next(tokens.Count)]);
            }
            default: // operation needs a non-empty string
                return s.Insert(pos, RandomChar(rand).ToString());
        }
    }

    /// <summary>Half JSON-significant characters, a quarter printable ASCII, a quarter any UTF-16 code unit.</summary>
    private static char RandomChar(Random rand) => rand.Next(4) switch
    {
        0 or 1 => JsonChars[rand.Next(JsonChars.Length)],
        2 => (char)rand.Next(0x20, 0x7F),
        _ => (char)rand.Next(0x10000),
    };
}
