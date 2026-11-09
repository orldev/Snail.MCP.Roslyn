namespace Snail.MCP.Roslyn.Domain;

internal sealed record SymbolReference(
    string File,
    int Line,
    int Column,
    string Kind,
    string Snippet);
