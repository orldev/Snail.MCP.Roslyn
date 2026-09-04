namespace Snail.MCP.Roslyn.Domain;

public sealed record SymbolReference(
    string File,
    int Line,
    int Column,
    string Kind,
    string Snippet);
