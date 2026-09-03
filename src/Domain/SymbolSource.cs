namespace Snail.MCP.Roslyn.Domain;

/// <summary>The source text that declares one symbol, cut to its own span.</summary>
public sealed record SymbolSource(
    string Id,
    string Name,
    string Kind,
    string? File,
    int StartLine,
    int EndLine,
    string Text);
