using System.Collections;
using System.Text;
using System.Text.Unicode;
using Fuzzers.Util;

namespace Fuzzers.Fuzz;

public class Fuzzer
{
    private FuzzerConfig _config { get; init; }
    private Random _random { get; init; }

    public Fuzzer(FuzzerConfig config, int? seed = null)
    {
        _config = config;
        _random = seed is null ? Random.Shared : new Random(seed.Value);
    }

    public string Fuzz(string input)
    {
        for (int i = 0; i < _config.Mutations; i++)
        {
            var index = _random.Next(0, input.Length);

            input = Mutate(input, index);
        }

        return input;
    }

    private string Mutate(string input, int start)
    {
        var mutators = Enum.GetValues<Fuzzes>();
        var mutationIndex = _random.Next(0, mutators.Length);
        var mutation = mutators[mutationIndex];
        var generatedMutation = GenerateReplacement();

        return mutation switch
        {
            Fuzzes.Delete => Delete(input, start, start),
            Fuzzes.Insert => Insert(input, start, generatedMutation),
            Fuzzes.Replace => Replace(input, start, start + generatedMutation.Length, generatedMutation),
            // TODO: Implement FullFlip and BitFlip
            // Fuzzes.Flip => Flip(input, start, true),
            // Fuzzes.BitFlip => Flip(input, start),
            _ => ""
        };
    }
    
    private string Flip(string input, int index, bool fullFlip = false)
    {
        var bytes = Encoding.UTF8.GetBytes(input[index].ToString());
        
        // TODO: Implement bitFlip
        if (!fullFlip)
        {
            var randByte = bytes[_random.Next(0, bytes.Length)];
            var bitIndex = (byte)(1 << _random.Next(0, 8));
            // return Encoding.UTF8.GetString(i);
        }

        for (int i = 0; i < bytes.Length; i++)
        {
            var value = bytes[i];
            bytes[i] = (byte)(value ^ value);
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private string Delete(string input, int start, int end)
    {
        return Replace(input, start, end, "");
    }

    private string GenerateReplacement()
    {
        if (!_config.GenerateFatMutations)
        {
            var index = _random.Next(0, Alphabet.PlainCharacters.Length);

            return Alphabet.PlainCharacters[index];
        }

        return "jfaæsldjfnq";  // TODO do something real
    }

    private string Insert(string input, int index, string insert)
    {
        var beforeSpan = input.AsSpan()[..index];
        var afterSpan = input.AsSpan()[(index + 1)..];

        return string.Concat(beforeSpan, insert, afterSpan);
    }

    private string Replace(string input, int start, int end, string replaceWith)
    {
        var beforeSpan = input.AsSpan()[..start];
        var afterSpan = input.AsSpan()[end..];

        return string.Concat(beforeSpan, replaceWith, afterSpan);
    }
}
