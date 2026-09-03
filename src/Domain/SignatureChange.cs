namespace Snail.MCP.Roslyn.Domain;

/// <summary>How a method signature should read after the change.</summary>
public sealed record SignatureChange(
    IReadOnlyList<string>? Parameters,
    string? ReturnType);
