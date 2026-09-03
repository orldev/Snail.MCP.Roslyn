using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Which namespace depends on which, so a cycle can be named rather than felt.</summary>
internal sealed class NamespaceGraph
{
    private readonly Dictionary<string, HashSet<string>> edges = new(StringComparer.Ordinal);

    public static async Task<NamespaceGraph> BuildAsync(Solution solution, CancellationToken cancellationToken)
    {
        var graph = new NamespaceGraph();

        foreach (var project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
        {
            foreach (var document in project.Documents)
            {
                if (document.FilePath is not { } file || SourcePaths.IsUnderOutput(file))
                    continue;

                await graph.WalkAsync(document, cancellationToken);
            }
        }

        return graph;
    }

    /// <summary>Every cycle rendered as the path that closes it.</summary>
    public IReadOnlyList<string> Cycles()
    {
        List<string> cycles = [];
        HashSet<string> settled = new(StringComparer.Ordinal);

        foreach (var start in edges.Keys)
            Walk(start, [], settled, cycles);

        return [.. cycles.Distinct(StringComparer.Ordinal)];
    }

    private void Walk(string node, List<string> path, HashSet<string> settled, List<string> cycles)
    {
        if (path.Contains(node, StringComparer.Ordinal))
        {
            cycles.Add(string.Join(" -> ", path[path.IndexOf(node)..].Append(node)));

            return;
        }

        if (!settled.Add(node) || !edges.TryGetValue(node, out var next))
            return;

        path.Add(node);

        foreach (var target in next)
            Walk(target, path, settled, cycles);

        path.RemoveAt(path.Count - 1);
    }

    private async Task WalkAsync(Document document, CancellationToken cancellationToken)
    {
        var model = await document.GetSemanticModelAsync(cancellationToken);
        var root = await document.GetSyntaxRootAsync(cancellationToken);

        if (model is null || root is null)
            return;

        foreach (var declaration in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            if (model.GetDeclaredSymbol(declaration, cancellationToken) is not { ContainingNamespace: { } owner } declared)
                continue;

            var from = owner.ToDisplayString();

            foreach (var name in declaration.DescendantNodes().OfType<SimpleNameSyntax>())
            {
                if (model.GetSymbolInfo(name, cancellationToken).Symbol is not { } used)
                    continue;

                Connect(from, used, declared);
            }
        }
    }

    private void Connect(string from, ISymbol used, ISymbol declared)
    {
        var target = used.Kind == SymbolKind.NamedType ? used : used.ContainingType;

        if (target is null || !target.Locations.Any(location => location.IsInSource))
            return;

        if (target.ContainingNamespace is not { } space || SymbolEqualityComparer.Default.Equals(target, declared))
            return;

        var to = space.ToDisplayString();

        if (string.Equals(from, to, StringComparison.Ordinal) || Nested(from, to) || Nested(to, from))
            return;

        if (!edges.TryGetValue(from, out var set))
            set = edges[from] = new HashSet<string>(StringComparer.Ordinal);

        set.Add(to);
    }

    private static bool Nested(string outer, string inner) =>
        inner.StartsWith($"{outer}.", StringComparison.Ordinal);
}
