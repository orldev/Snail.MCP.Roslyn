namespace Snail.MCP.Roslyn.Domain;

/// <summary>The outcome of one script evaluated against the loaded solution.</summary>
public sealed record ScriptResult(
    bool Succeeded,
    string? Value,
    string Output,
    IReadOnlyList<string> Errors,
    long ElapsedMilliseconds,
    IReadOnlyList<string> Notes);
