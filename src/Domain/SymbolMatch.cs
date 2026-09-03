namespace Snail.MCP.Roslyn.Domain;

/// <summary>One candidate returned by a name search, carrying the identifier every other tool expects.</summary>
public sealed record SymbolMatch(
    string Id,
    string Name,
    string Kind,
    string? Container,
    string Accessibility,
    string? File,
    int Line,
    bool IsGenerated);
