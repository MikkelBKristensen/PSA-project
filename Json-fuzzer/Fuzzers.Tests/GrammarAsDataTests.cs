using System.Text.Json;
using Fuzzers.Grammar;
using Xunit.Abstractions;

namespace Fuzzers.Tests;

public class GrammarAsDataTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public void GenerateValidInput()
    {
        var json = JsonAsData.GenerateJson(10, 10, 10);
        try
        {
            JsonDocument.Parse(json);
            outputHelper.WriteLine(json);
        }
        catch (Exception e)
        {
            Assert.Fail($"{e.Message}\n{json}");
        }
    }
    
}