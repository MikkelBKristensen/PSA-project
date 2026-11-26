using Fuzzers.Fuzz;
using Xunit.Abstractions;

namespace Fuzzers.Tests;

public class MutationFuzzerTests(ITestOutputHelper outputHelper)
{
    private static FuzzerConfig config => new FuzzerConfig
    {
        Mutations = 10,
        GenerateFatMutations = false
    };
    
    private Fuzzer _fuzzer = new(config);
    
    [Fact]
    public void FuzzString_FuzzesAStringLol()
    {
        const string input = "Hello World";
        var fuzzedString = _fuzzer.Fuzz(input);
        
        outputHelper.WriteLine(fuzzedString);
        Assert.NotEmpty(fuzzedString);
        Assert.NotEqual(input, fuzzedString);
    }
}