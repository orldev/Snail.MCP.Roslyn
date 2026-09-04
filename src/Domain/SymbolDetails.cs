namespace Snail.MCP.Roslyn.Domain;

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
