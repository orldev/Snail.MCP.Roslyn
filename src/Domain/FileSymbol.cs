namespace Snail.MCP.Roslyn.Domain;

/// <summary>One declaration a file holds, carrying the identifier the symbol tools expect and its use count.</summary>
internal sealed record FileSymbol(
    string Id,
    string Name,
    string Kind,
    string? Container,
    string Accessibility,
    int Line,
    int References);
