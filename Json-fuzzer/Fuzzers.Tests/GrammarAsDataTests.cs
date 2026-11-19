using System.Text.Json;
using System.Text.Json.Nodes;
using Fuzzers.Config;
using Fuzzers.Grammar;
using Xunit.Abstractions;

namespace Fuzzers.Tests;

public class GrammarAsDataTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public void CrazyJsonInput()
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

    [Fact]
    public void ObjectAlwaysEmpty()
    {
        var options = new GeneratorConfig
        {
            ObjectMembersWeight = 0,
        };

        for (int i = 0; i < 10000; i++)
        {
            var json = JsonAsData.GenerateJson(10, 10, 10, options);
            try
            {
                var isEmpty = JsonNode.Parse(json) is JsonObject o && o.Count == 0;

                Assert.True(isEmpty);
            }
            catch (Exception e)
            {
                Assert.Fail($"{e.Message}\n{json}");
            }

        }
    }
}
