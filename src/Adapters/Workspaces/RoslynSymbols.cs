using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

internal sealed class RoslynSymbols(IPathMap paths, SolutionCache cache) : ISymbolLookup
{
    private readonly SymbolResolver resolver = new(paths);

    public async Task<IReadOnlyList<SymbolMatch>> SearchAsync(
        string solutionId,
        string query,
        SymbolKindFilter kind,
        int limit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var cap = Math.Clamp(limit, 1, 200);

        var found = await SymbolFinder.FindSourceDeclarationsAsync(
            solution,
            name => name.Contains(query, StringComparison.OrdinalIgnoreCase),
            SymbolFilter.TypeAndMember,
            cancellationToken);

        List<ISymbol> all = [.. found];
        all.AddRange(await GeneratedAsync(solution, query, all, cancellationToken));

        var markup = await MarkupMap.BuildAsync(solution, cancellationToken);

        return
        [
            .. all
                .Where(symbol => SymbolFacts.Matches(symbol, kind))
                .Select(symbol => Authored(SymbolFacts.Match(symbol, paths), markup))
                .OrderBy(match => Distance(match.Name, query))
                .ThenBy(match => match.Name, StringComparer.Ordinal)
                .Take(cap)
        ];
    }

    /// <summary>A component type declares itself through its markup file: point the match there, not into obj/.</summary>
    private SymbolMatch Authored(SymbolMatch match, IReadOnlyDictionary<string, string> markup)
    {
        if (match.File is null || !markup.TryGetValue(paths.ToProcess(match.File), out var authored))
            return match;

        return match with { File = paths.ToCaller(authored), Line = 0 };
    }

    private static async Task<IReadOnlyList<ISymbol>> GeneratedAsync(
        Solution solution,
        string query,
        List<ISymbol> already,
        CancellationToken cancellationToken)
    {
        HashSet<string> seen = [.. already.Select(SymbolFacts.IdOf)];
        List<ISymbol> generated = [];

        foreach (var project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);

            if (compilation is null)
                continue;

            foreach (var symbol in compilation.GetSymbolsWithName(
                         name => name.Contains(query, StringComparison.OrdinalIgnoreCase),
                         SymbolFilter.TypeAndMember,
                         cancellationToken))
            {
                if (symbol.IsImplicitlyDeclared || symbol is IMethodSymbol { AssociatedSymbol: not null })
                    continue;

                if (seen.Add(SymbolFacts.IdOf(symbol)))
                    generated.Add(symbol);
            }
        }

        return generated;
    }

    public async Task<SymbolDetails?> DescribeAsync(
        string solutionId,
        SymbolQuery query,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var symbol = await resolver.ResolveAsync(solution, query, cancellationToken);

        return symbol is null ? null : SymbolFacts.Details(symbol, paths);
    }

    public async Task<SymbolSource?> ReadAsync(
        string solutionId,
        SymbolQuery query,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var symbol = await resolver.ResolveAsync(solution, query, cancellationToken);
        var reference = symbol?.DeclaringSyntaxReferences.FirstOrDefault();

        if (symbol is null || reference is null)
            return null;

        var node = await reference.GetSyntaxAsync(cancellationToken);
        var tree = node.SyntaxTree;
        var span = tree.GetLineSpan(node.FullSpan);
        var file = tree.FilePath;

        return new SymbolSource(
            SymbolFacts.IdOf(symbol),
            symbol.Name,
            symbol.Kind.ToString(),
            string.IsNullOrEmpty(file) ? null : paths.ToCaller(file),
            span.StartLinePosition.Line + 1,
            span.EndLinePosition.Line + 1,
            node.ToFullString().Trim('\r', '\n'));
    }

    private static int Distance(string name, string query) =>
        name.Equals(query, StringComparison.Ordinal) ? 0
        : name.Equals(query, StringComparison.OrdinalIgnoreCase) ? 1
        : name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 2
        : 3;
}
