using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>What a script sees: the loaded solution and the few helpers that save it from boilerplate.</summary>
/// <remarks>Public because a script is compiled into an assembly of its own, which sees its globals only when their type is public.</remarks>
public sealed class ScriptContext
{
    private readonly StringBuilder _output = new();

    private ScriptContext(Solution solution, IReadOnlyList<Compilation> compilations)
    {
        Solution = solution;
        Compilations = compilations;
    }

    /// <summary>The loaded solution, as fresh as the files on disk when the script started.</summary>
    public Solution Solution { get; }

    /// <summary>What the script printed so far.</summary>
    public string Output => _output.ToString();

    /// <summary>The compilation of every C# project of the solution.</summary>
    public IReadOnlyList<Compilation> Compilations { get; }

    /// <summary>Builds the context, awaiting the compilations rather than blocking a request thread on them.
    /// They are cached on the snapshot, so a script pays for them only when nothing else has yet.</summary>
    public static async Task<ScriptContext> CreateAsync(Solution solution, CancellationToken cancellationToken)
    {
        List<Compilation> compilations = [];

        foreach (var project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
        {
            if (await project.GetCompilationAsync(cancellationToken) is { } compilation)
                compilations.Add(compilation);
        }

        return new ScriptContext(solution, compilations);
    }

    /// <summary>Adds a value to the answer, a sequence one item a line and at most 200 of them.</summary>
    public void Print(object? value) => _output.AppendLine(Render(value));

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

    /// <summary>Every reference to the symbol across the solution.</summary>
    public Task<IEnumerable<ReferencedSymbol>> Refs(ISymbol symbol) =>
        SymbolFinder.FindReferencesAsync(symbol, Solution);

    /// <summary>Every implementation of an interface, an interface member or an abstract member.</summary>
    public Task<IEnumerable<ISymbol>> Impls(ISymbol symbol) =>
        SymbolFinder.FindImplementationsAsync(symbol, Solution);

    /// <summary>Every member that calls the symbol, with the places it does.</summary>
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
