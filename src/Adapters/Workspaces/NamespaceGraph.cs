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

    /// <summary>Every cycle, rendered as the namespaces that take part in it.</summary>
    /// <remarks>Found as strongly connected components rather than by depth-first search with a shared visited
    /// set: that set makes a node reachable only once, so every cycle running through a node the first descent
    /// already settled goes unreported. A component of more than one namespace is exactly a cycle among them,
    /// and each is reported once however many ways round it there are.</remarks>
    public IReadOnlyList<string> Cycles()
    {
        List<string> cycles = [];
        Dictionary<string, int> index = new(StringComparer.Ordinal);
        Dictionary<string, int> low = new(StringComparer.Ordinal);
        HashSet<string> onStack = new(StringComparer.Ordinal);
        Stack<string> stack = new();
        var next = 0;

        void Connect(string node)
        {
            index[node] = low[node] = next++;
            stack.Push(node);
            onStack.Add(node);

            foreach (var target in edges.TryGetValue(node, out var found) ? found : [])
            {
                if (!index.ContainsKey(target))
                {
                    Connect(target);
                    low[node] = Math.Min(low[node], low[target]);
                }
                else if (onStack.Contains(target))
                {
                    low[node] = Math.Min(low[node], index[target]);
                }
            }

            if (low[node] != index[node])
                return;

            List<string> component = [];
            string member;

            do
            {
                member = stack.Pop();
                onStack.Remove(member);
                component.Add(member);
            }
            while (!string.Equals(member, node, StringComparison.Ordinal));

            if (component.Count > 1)
                cycles.Add(string.Join(" -> ", component.Order(StringComparer.Ordinal)));
        }

        foreach (var start in edges.Keys.Order(StringComparer.Ordinal))
        {
            if (!index.ContainsKey(start))
                Connect(start);
        }

        return [.. cycles.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
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
