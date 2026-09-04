namespace Snail.MCP.Roslyn.Domain;

public sealed record TypeHierarchy(
    string SymbolId,
    string Name,
    IReadOnlyList<SymbolMatch> BaseTypes,
    IReadOnlyList<SymbolMatch> Interfaces,
    IReadOnlyList<SymbolMatch> Derived,
    IReadOnlyList<string> Notes);
