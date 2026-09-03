namespace Snail.MCP.Roslyn.Domain;

/// <summary>Where a type sits between the types it inherits and the types that inherit it.</summary>
public sealed record TypeHierarchy(
    string SymbolId,
    string Name,
    IReadOnlyList<SymbolMatch> BaseTypes,
    IReadOnlyList<SymbolMatch> Interfaces,
    IReadOnlyList<SymbolMatch> Derived,
    IReadOnlyList<string> Notes);
