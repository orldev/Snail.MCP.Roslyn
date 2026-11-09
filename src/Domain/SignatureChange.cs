namespace Snail.MCP.Roslyn.Domain;

internal sealed record SignatureChange(
    IReadOnlyList<string>? Parameters,
    string? ReturnType);
