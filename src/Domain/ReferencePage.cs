namespace Snail.MCP.Roslyn.Domain;

public sealed record ReferencePage(
    string SymbolId,
    IReadOnlyList<SymbolReference> Items,
    int Total,
    bool Truncated,
    IReadOnlyList<string> Notes);
