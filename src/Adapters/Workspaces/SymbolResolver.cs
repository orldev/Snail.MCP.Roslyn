using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Turns an identifier, a bare name or a source position into the symbol the compiler means.</summary>
internal sealed class SymbolResolver(IPathMap paths)
{
    private const int CandidateCap = 20;

    public async Task<ISymbol?> ResolveAsync(Solution solution, SymbolQuery query, CancellationToken cancellationToken)
    {
        if (query.HasPosition)
            return await AtPositionAsync(solution, query, cancellationToken);

        if (string.IsNullOrWhiteSpace(query.Id))
            return null;

        return await ByIdAsync(solution, query.Id, cancellationToken);
    }

    /// <summary>Resolves the symbol, or returns the candidates when a bare name matches more than one.</summary>
    /// <remarks>A name is looked up by name rather than by predicate: the predicate overload asks every declaration in
    /// the solution, the name overload asks the index Roslyn already keeps.</remarks>
    public async Task<(ISymbol? Symbol, IReadOnlyList<SymbolMatch> Candidates)> PickAsync(
        Solution solution,
        SymbolQuery query,
        CancellationToken cancellationToken)
    {
        if (!query.HasName)
            return (await ResolveAsync(solution, query, cancellationToken), []);

        var found = await SymbolFinder.FindSourceDeclarationsAsync(
            solution,
            query.Name!,
            ignoreCase: false,
            SymbolFilter.TypeAndMember,
            cancellationToken);

        List<ISymbol> exact = [.. found.Where(symbol => symbol.Locations.Any(location => location.IsInSource))];

        if (exact.Count == 1)
            return (exact[0], []);

        return (null, [.. exact.Take(CandidateCap).Select(symbol => SymbolFacts.Match(symbol, paths))]);
    }

    private static async Task<ISymbol?> ByIdAsync(Solution solution, string id, CancellationToken cancellationToken)
    {
        foreach (var project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);

            if (compilation is null)
                continue;

            var symbol = DocumentationCommentId.GetFirstSymbolForDeclarationId(id, compilation);

            if (symbol is not null)
                return symbol;
        }

        return null;
    }

    private async Task<ISymbol?> AtPositionAsync(Solution solution, SymbolQuery query, CancellationToken cancellationToken)
    {
        var path = paths.ToProcess(query.File!);
        var documentId = solution.GetDocumentIdsWithFilePath(path).FirstOrDefault();

        if (documentId is null || solution.GetDocument(documentId) is not { } document)
            return null;

        var text = await document.GetTextAsync(cancellationToken);

        if (query.Line < 1 || query.Line > text.Lines.Count)
            return null;

        var line = text.Lines[query.Line - 1];
        var position = Math.Min(line.Start + Math.Max(query.Column - 1, 0), line.End);

        return await SymbolFinder.FindSymbolAtPositionAsync(document, position, cancellationToken);
    }
}
