using Microsoft.CodeAnalysis;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Everything the solution-wide indexes need from one syntax tree, read in one pass over it.</summary>
internal sealed record DocumentFacts(
    IReadOnlyList<ISymbol> Declarations,
    IReadOnlyList<SymbolUse> Uses,
    IReadOnlyList<NamespaceEdge> Edges,
    IReadOnlyList<DeclaredNamespace> Namespaces,
    IReadOnlyList<RegistrationFact> Registrations);

/// <summary>One container registration as the call site states it, before its file is named.</summary>
internal readonly record struct RegistrationFact(
    string Contract,
    string? Implementation,
    string Lifetime,
    string Method,
    int Line,
    string Text);

internal readonly record struct SymbolUse(ISymbol Symbol, int Count);

internal readonly record struct NamespaceEdge(string From, string To);

internal readonly record struct DeclaredNamespace(string Name, int Line);
