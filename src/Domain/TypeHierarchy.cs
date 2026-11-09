namespace Snail.MCP.Roslyn.Domain;

internal sealed record TypeHierarchy(
    string SymbolId,
    string Name,
    IReadOnlyList<SymbolMatch> BaseTypes,
    IReadOnlyList<SymbolMatch> Interfaces,
    IReadOnlyList<SymbolMatch> Derived,
    IReadOnlyList<string> Notes);
