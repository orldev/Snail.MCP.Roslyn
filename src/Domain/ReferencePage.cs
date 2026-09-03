namespace Snail.MCP.Roslyn.Domain;

/// <summary>A budgeted slice of references; truncation is always stated, never silent.</summary>
public sealed record ReferencePage(
    string SymbolId,
    IReadOnlyList<SymbolReference> Items,
    int Total,
    bool Truncated,
    IReadOnlyList<string> Notes);
