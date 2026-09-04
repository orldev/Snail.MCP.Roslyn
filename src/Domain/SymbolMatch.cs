namespace Snail.MCP.Roslyn.Domain;

/// <summary>One candidate from a name search, carrying the id the other tools address symbols by.</summary>
public sealed record SymbolMatch(
    string Id,
    string Name,
    string Kind,
    string? Container,
    string Accessibility,
    string? File,
    int Line,
    bool IsGenerated);
