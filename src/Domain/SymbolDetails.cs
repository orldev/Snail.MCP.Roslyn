namespace Snail.MCP.Roslyn.Domain;

/// <summary>Everything the compiler knows about one symbol.</summary>
public sealed record SymbolDetails(
    string Id,
    string Name,
    string Kind,
    string Signature,
    string Accessibility,
    string? Container,
    string? Namespace,
    string? File,
    int Line,
    string? Documentation,
    bool IsGenerated);
