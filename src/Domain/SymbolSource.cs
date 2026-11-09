namespace Snail.MCP.Roslyn.Domain;

internal sealed record SymbolSource(
    string Id,
    string Name,
    string Kind,
    string? File,
    int StartLine,
    int EndLine,
    string Text);
