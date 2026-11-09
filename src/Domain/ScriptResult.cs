namespace Snail.MCP.Roslyn.Domain;

internal sealed record ScriptResult(
    bool Succeeded,
    string? Value,
    string Output,
    IReadOnlyList<string> Errors,
    long ElapsedMilliseconds,
    IReadOnlyList<string> Notes);
