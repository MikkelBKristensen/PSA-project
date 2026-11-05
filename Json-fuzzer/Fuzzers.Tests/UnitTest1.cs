using System.Text.Json;
using Xunit.Abstractions;

namespace Fuzzers.Tests;
using Fuzzers.Grammar;


public class UnitTest1(ITestOutputHelper outputHelper)
{
    [Fact]
    public void GenerateJsonObject()
    {
        var obj = GrammarFuzzer.GenerateJson();
        outputHelper.WriteLine(obj);
        
    }

    [Fact]
    public void TryParseOutputJson()
    {
        var obj = GrammarFuzzer.GenerateJson();

        try
        {
            JsonDocument.Parse(obj);
        }
        catch (Exception e)
        {
            Assert.Fail(e.Message);   
        }
    }
}