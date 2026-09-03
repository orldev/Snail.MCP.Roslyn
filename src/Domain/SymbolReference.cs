namespace Snail.MCP.Roslyn.Domain;

/// <summary>One place where a symbol is used, with the source line for orientation.</summary>
public sealed record SymbolReference(
    string File,
    int Line,
    int Column,
    string Kind,
    string Snippet);
