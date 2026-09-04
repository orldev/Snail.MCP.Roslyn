using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>What a script sees: the loaded solution and the few helpers that save it from boilerplate.</summary>
public sealed class ScriptContext(Solution solution)
{
    private readonly StringBuilder output = new();

    public Solution Solution { get; } = solution;

    public string Output => output.ToString();

    public IReadOnlyList<Compilation> Compilations { get; } =
    [
        .. solution.Projects
            .Where(project => project.Language == LanguageNames.CSharp)
            .Select(project => project.GetCompilationAsync().GetAwaiter().GetResult())
            .OfType<Compilation>()
    ];

    public void Print(object? value) => output.AppendLine(Render(value));

    /// <summary>Types declared in source whose name contains the pattern.</summary>
    public IEnumerable<INamedTypeSymbol> Types(string pattern) =>
        Compilations
            .SelectMany(compilation => compilation.GetSymbolsWithName(
                name => name.Contains(pattern, StringComparison.OrdinalIgnoreCase), SymbolFilter.Type))
            .OfType<INamedTypeSymbol>()
            .Where(type => type.Locations.Any(location => location.IsInSource));

    /// <summary>The symbol behind a documentation comment id, the same id every tool returns.</summary>
    public ISymbol? Sym(string documentationId) =>
        Compilations
            .Select(compilation => DocumentationCommentId.GetFirstSymbolForDeclarationId(documentationId, compilation))
            .FirstOrDefault(symbol => symbol is not null);

    public Task<IEnumerable<ReferencedSymbol>> Refs(ISymbol symbol) =>
        SymbolFinder.FindReferencesAsync(symbol, Solution);

    public Task<IEnumerable<ISymbol>> Impls(ISymbol symbol) =>
        SymbolFinder.FindImplementationsAsync(symbol, Solution);

    public Task<IEnumerable<SymbolCallerInfo>> Callers(ISymbol symbol) =>
        SymbolFinder.FindCallersAsync(symbol, Solution);

    private static string Render(object? value) => value switch
    {
        null => "null",
        string text => text,
        System.Collections.IEnumerable items => string.Join(Environment.NewLine, items.Cast<object?>().Take(200).Select(Render)),
        _ => value.ToString() ?? string.Empty
    };
}
