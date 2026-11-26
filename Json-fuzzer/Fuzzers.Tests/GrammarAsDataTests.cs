using System.Text.Json;
using System.Text.Json.Nodes;
using Fuzzers.Config;
using Fuzzers.Grammar;
using Xunit.Abstractions;

namespace Fuzzers.Tests;

public class GrammarAsDataTests(ITestOutputHelper outputHelper)
{
    private static readonly int _seed = 87686338;

    private static int MaxWidth(JsonNode? node) => node switch
    {
        JsonObject obj => Math.Max(obj.Count, obj.Select(kv => MaxWidth(kv.Value)).DefaultIfEmpty(0).Max()),
        JsonArray arr => Math.Max(arr.Count, arr.Select(MaxWidth).DefaultIfEmpty(0).Max()),
        _ => 0,
    };

    [Fact]
    public void CrazyJsonInput()
    {
        var config = new GeneratorConfig();

        var json = JsonAsData.GenerateJson(config);
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
    public void WidthOfOne_NeverExeeds_WidthOfOne()
    {
        var options = GeneratorConfig.Default() with
        {
            MaxWidth = 1
        };

        var json = JsonAsData.GenerateJson(options, _seed);
        var jsonNode = JsonNode.Parse(json);

        outputHelper.WriteLine(jsonNode.ToString());

        Assert.Equal(1, MaxWidth(jsonNode));
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
            var json = JsonAsData.GenerateJson(options);
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
        var options = GeneratorConfig.Default() with
        {
            ObjectEmptyWeight = 0,
            MaxDepth = 10,
        };

        for (int i = 0; i < 10000; i++)
        {
            var json = JsonAsData.GenerateJson(options);
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


        var json = JsonAsData.GenerateJson(options);

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
            var json = JsonAsData.GenerateJson(options);
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

    [Fact]
    public void FuzzingTest()
    {
        var options = new GeneratorConfig();

        for (int i = 0; i < 100000; i++)
        {
            var seed = _seed + i;
            var json = JsonAsData.GenerateJson(options, seed);
            try
            {
                var jsonDocument = JsonNode.Parse(json);
            }
            catch (Exception e)
            {
                Assert.Fail($"{e.Message}\n{json}");
            }
        }
    }

    [Fact]
    public void VisualOutputTest()
    {
        var options = GeneratorConfig.Default();

        var json = JsonAsData.GenerateJson(options, _seed);

        try
        {
            var jsonObject = JsonNode.Parse(json);
            outputHelper.WriteLine(jsonObject.ToString());

            Assert.True(json.Length > 10);
        }
        catch (Exception e)
        {
            Assert.Fail($"{e.Message}\n{json}");
        }
    }

    [Fact]
    public void FuzzConfigTest()
    {
        for (int i = 0; i < 40; i++)
        {
            var seed = _seed + i;
            var options = GeneratorConfig.RandomConfig(seed);

            for (int j = 0; j < 1000; j++)
            {
                var innerSeed = seed + j;

                var json = JsonAsData.GenerateJson(options, innerSeed);

                try
                {
                    var jsonDocument = JsonNode.Parse(json);
                }
                catch (Exception e)
                {
                    Assert.Fail($"{e.Message}\n{json}");
                }
            }
        }
    }
}
