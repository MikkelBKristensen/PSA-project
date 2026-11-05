using Fuzzers.Harness;
using Xunit.Abstractions;

namespace Fuzzers.Tests;

public class HarnessTests(ITestOutputHelper output)
{
    private static readonly IJsonParser Stj = new SystemTextJsonParser();
    private static readonly IJsonParser Newtonsoft = new NewtonsoftJsonParser();
    private static readonly IJsonParser[] Both = [Stj, Newtonsoft];

    [Theory]
    [InlineData("{\"a\":[1,2.5e3,true,null,\"x\"]}")]
    [InlineData("[]")]
    [InlineData("\"\\u00e9\"")]
    public void ValidJsonIsAcceptedByBoth(string input)
    {
        Assert.Equal(Verdict.Accepted, Stj.Parse(input).Verdict);
        Assert.Equal(Verdict.Accepted, Newtonsoft.Parse(input).Verdict);
    }

    [Theory]
    [InlineData("{\"a\" 1}")]
    [InlineData("[1,2")]
    [InlineData("")]
    public void InvalidJsonIsRejectedByBoth(string input)
    {
        Assert.Equal(Verdict.Rejected, Stj.Parse(input).Verdict);
        Assert.Equal(Verdict.Rejected, Newtonsoft.Parse(input).Verdict);
    }

    [Fact]
    public void LoneSurrogateCrashesSystemTextJson()
    {
        var outcome = Stj.Parse("\"" + (char)0xD800 + "\"");
        output.WriteLine(outcome.ToString());
        Assert.Equal(Verdict.Crashed, outcome.Verdict);
        Assert.Equal(nameof(ArgumentException), outcome.ErrorType);
        Assert.NotNull(outcome.Location);
    }

    [Fact]
    public void SingleQuotesAreADisagreement()
    {
        // Newtonsoft is lenient by default; System.Text.Json follows RFC 8259.
        var outcomes = Both.Select(p => p.Parse("{'a':1}")).ToArray();
        var signatures = Findings.Signatures(Both, outcomes).ToList();
        signatures.ForEach(output.WriteLine);

        var signature = Assert.Single(signatures);
        Assert.StartsWith("disagree | accepted by Newtonsoft | SystemTextJson:", signature);
    }

    [Fact]
    public void SameErrorAtDifferentPositionsGetsSameSignature()
    {
        string Sig(string input) => Findings.Signatures(Both, Both.Select(p => p.Parse(input)).ToArray()).Single();
        Assert.Equal(Sig("{'a':1}"), Sig("[1, 2, {'bcd': [3]}]"));
    }

    [Theory]
    [InlineData("Cannot transcode invalid UTF-16 string to UTF-8 JSON text.", "Cannot transcode invalid UTF-16 string to UTF-8 JSON text.")]
    [InlineData("''' is an invalid start of a value. LineNumber: 0 | BytePositionInLine: 12.", "'_' is an invalid start of a value.")]
    [InlineData("JSON integer 123456789012 is too large to parse. Path '[0]', line 1, position 13.", "JSON integer 0 is too large to parse.")]
    [InlineData("Unexpected character encountered while parsing value: N. Path '', line 1, position 1.", "Unexpected character encountered while parsing value: _")]
    [InlineData("Invalid Unicode escape sequence: \\ullx. Path '', line 1, position 7.", "Invalid Unicode escape sequence: _")]
    [InlineData("Error parsing comment. Expected: *, got ]. Path '', line 1, position 2.", "Error parsing comment. Expected: _")]
    [InlineData("'t }': 'NaN' is an invalid JSON literal. Expected the literal 'true'. LineNumber: 0 | BytePositionInLine: 1.", "'_' is an invalid JSON literal. Expected the literal '_'.")]
    public void NormalizeMessageRemovesInputSpecificParts(string message, string expected)
    {
        Assert.Equal(expected, Findings.NormalizeMessage(message));
    }

    [Fact]
    public void AgreementIsNotAFinding()
    {
        Assert.Empty(Findings.Signatures(Both, Both.Select(p => p.Parse("[1,2]")).ToArray()));
        Assert.Empty(Findings.Signatures(Both, Both.Select(p => p.Parse("[1,2")).ToArray()));
    }

    [Fact]
    public void EscapeKeepsSurrogatePairsAndEscapesTheRest()
    {
        Assert.Equal("a😀\\u0000\\uD800\\\\", Findings.Escape("a😀" + (char)0 + (char)0xD800 + "\\"));
    }
}
