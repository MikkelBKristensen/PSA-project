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
        var config = new GeneratorConfig();

        var json = JsonAsData.GenerateJson(10, 10, config);
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
            var json = JsonAsData.GenerateJson(10, 10, options);
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

    [Fact]
    public void ObjectNeverEmpty()
    {
        var options = new GeneratorConfig
        {
            ObjectEmptyWeight = 0,
            MaxDepth = 10,
        };

        for (int i = 0; i < 10000; i++)
        {
            var json = JsonAsData.GenerateJson(10, 10, options);
            try
            {
                var isEmpty = JsonNode.Parse(json) is JsonObject o && o.Count > 0;

                Assert.True(isEmpty);
            }
            catch (Exception e)
            {
                Assert.Fail($"{e.Message}\n{json}");
            }

        }
    }

    [Fact]
    public void CharWeightTest()
    {
        var options = new GeneratorConfig
        {
            CharactersMultipleWeight = 100,
            ObjectEmptyWeight = 0,
            MaxDepth = 10,
        };


        var json = JsonAsData.GenerateJson(10, 10, options);

        try
        {
            JsonNode.Parse(json);
            outputHelper.WriteLine(json);

            Assert.True(json.Length > 10);
        }
        catch (Exception e)
        {
            Assert.Fail($"{e.Message}\n{json}");
        }

    }


    [Fact]
    public void CharWeightedHigh()
    {
        var options = new GeneratorConfig
        {
            CharactersMultipleWeight = 20,
            ObjectEmptyWeight = 0,
            MaxDepth = 10,
        };

        for (int i = 0; i < 10000; i++)
        {
            var json = JsonAsData.GenerateJson(10, 10, options);
            try
            {
                JsonNode.Parse(json);
            }
            catch (Exception e)
            {
                Assert.Fail($"{e.Message}\n{json}");
            }

        }
    }

}
