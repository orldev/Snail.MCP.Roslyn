using Microsoft.CodeAnalysis;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Counts every use of every source declaration, so solution-wide questions cost no walk of their own.</summary>
internal sealed class SolutionUsage
{
    private readonly Dictionary<string, UsageEntry> _entries = new(StringComparer.Ordinal);

    public IReadOnlyCollection<UsageEntry> Entries => _entries.Values;

    /// <summary>The symbol a declaration registers under, or null when its kind is not one this index tracks.</summary>
    public static ISymbol? Declares(ISymbol symbol) =>
        IsTracked(symbol) ? symbol.OriginalDefinition : null;

    /// <summary>The symbol a name counts against: an extension call counts for the method as declared, and a
    /// symbol declared outside the source is nobody's declaration to report.</summary>
    public static ISymbol? Counts(ISymbol symbol)
    {
        var target = symbol is IMethodSymbol { ReducedFrom: { } reduced } ? reduced : symbol;

        if (!IsTracked(target))
            return null;

        var definition = target.OriginalDefinition;

        return definition.Locations.Any(location => location.IsInSource) ? definition : null;
    }

    public void Add(DocumentFacts facts, string project, string file)
    {
        foreach (var declared in facts.Declarations)
            Entry(declared);

        foreach (var (symbol, count) in facts.Uses)
        {
            var entry = Entry(symbol);

            entry.References += count;
            entry.Projects.Add(project);
            entry.Files.Add(file);
        }
    }

    public UsageEntry? For(ISymbol symbol) =>
        _entries.GetValueOrDefault(SymbolFacts.IdOf(symbol.OriginalDefinition));

    public int CountFor(ISymbol symbol) => For(symbol)?.References ?? 0;

    private static bool IsTracked(ISymbol symbol) =>
        symbol.Kind is SymbolKind.NamedType or SymbolKind.Method or SymbolKind.Property
            or SymbolKind.Field or SymbolKind.Event;

    private UsageEntry Entry(ISymbol symbol)
    {
        var id = SymbolFacts.IdOf(symbol);

        if (_entries.TryGetValue(id, out var existing))
            return existing;

        var created = new UsageEntry(symbol);
        _entries[id] = created;

        return created;
    }
}
