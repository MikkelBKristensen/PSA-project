using System.Diagnostics;
using System.Reflection;
using Newtonsoft.Json.Linq;
using NewtonsoftException = Newtonsoft.Json.JsonException;
using StjException = System.Text.Json.JsonException;

namespace Fuzzers.Harness;

public enum Verdict
{
    /// <summary>Parsed without error.</summary>
    Accepted,

    /// <summary>Rejected with the parser's own, documented error type.</summary>
    Rejected,

    /// <summary>Threw anything else: a robustness bug, or at least an API contract violation.</summary>
    Crashed,
}

/// <param name="ErrorType">Exception type name, if any.</param>
/// <param name="Message">Exception message, if any.</param>
/// <param name="Location">For crashes: the innermost parser method on the stack.</param>
public readonly record struct Outcome(Verdict Verdict, string? ErrorType = null, string? Message = null, string? Location = null)
{
    public override string ToString() => Verdict == Verdict.Accepted
        ? "Accepted"
        : $"{Verdict} ({ErrorType}{(Location is null ? "" : " at " + Location)}: {Message})";
}

public interface IJsonParser
{
    string Name { get; }
    Outcome Parse(string input);
}

/// <summary>Shared try/catch logic: classify an exception as Rejected or Crashed.</summary>
public abstract class JsonParserBase : IJsonParser
{
    public abstract string Name { get; }
    protected abstract Assembly ParserAssembly { get; }
    protected abstract void ParseCore(string input);
    protected abstract bool IsOwnError(Exception e);

    public Outcome Parse(string input)
    {
        try
        {
            ParseCore(input);
            return new Outcome(Verdict.Accepted);
        }
        catch (Exception e) when (IsOwnError(e))
        {
            return new Outcome(Verdict.Rejected, e.GetType().Name, e.Message);
        }
        catch (Exception e)
        {
            return new Outcome(Verdict.Crashed, e.GetType().Name, e.Message, InnermostParserFrame(e));
        }
    }

    private string? InnermostParserFrame(Exception e)
    {
        var method = new StackTrace(e, false).GetFrames()
            .Select(f => f.GetMethod())
            .FirstOrDefault(m => m?.DeclaringType?.Assembly == ParserAssembly);
        return method is null ? null : $"{method.DeclaringType!.Name}.{method.Name}";
    }
}

/// <summary>System.Text.Json DOM parsing with default options.</summary>
public sealed class SystemTextJsonParser : JsonParserBase
{
    public override string Name => "SystemTextJson";
    protected override Assembly ParserAssembly => typeof(System.Text.Json.JsonDocument).Assembly;
    protected override bool IsOwnError(Exception e) => e is StjException;

    protected override void ParseCore(string input)
    {
        using var _ = System.Text.Json.JsonDocument.Parse(input);
    }
}

/// <summary>Newtonsoft.Json DOM parsing (<see cref="JToken.Parse(string)"/>) with default settings.</summary>
public sealed class NewtonsoftJsonParser : JsonParserBase
{
    public override string Name => "Newtonsoft";
    protected override Assembly ParserAssembly => typeof(JToken).Assembly;
    protected override bool IsOwnError(Exception e) => e is NewtonsoftException;
    protected override void ParseCore(string input) => JToken.Parse(input);
}

public static class JsonParsers
{
    public static IJsonParser[] All() => [new SystemTextJsonParser(), new NewtonsoftJsonParser()];
}
