namespace Snail.MCP.Roslyn.Domain;

public sealed record SignatureChange(
    IReadOnlyList<string>? Parameters,
    string? ReturnType);
