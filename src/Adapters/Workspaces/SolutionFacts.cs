using Microsoft.CodeAnalysis;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Every index a solution-wide question needs, assembled from one walk of each tree.</summary>
internal sealed class SolutionFacts
{
    private readonly Dictionary<DocumentId, IReadOnlyList<DeclaredNamespace>> _namespaces = [];

    private readonly List<(string File, RegistrationFact Registration)> _registrations = [];

    private SolutionFacts()
    {
    }

    public SolutionUsage Usage { get; } = new();

    public NamespaceGraph Graph { get; } = new();

    /// <summary>Every container registration in the solution, each with the file that states it.</summary>
    public IReadOnlyList<(string File, RegistrationFact Registration)> Registrations => _registrations;

    public static async Task<SolutionFacts> BuildAsync(Solution solution, CancellationToken cancellationToken)
    {
        var facts = new SolutionFacts();
        var markup = await MarkupMap.BuildAsync(solution, cancellationToken);

        foreach (var project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
        {
            var compilation = await project.GetCompilationAsync(cancellationToken);

            if (compilation is null)
                continue;

            foreach (var document in project.Documents)
            {
                if (document.FilePath is not { } file || SourcePaths.IsUnderOutput(file))
                    continue;

                if (await document.GetSyntaxTreeAsync(cancellationToken) is not { } tree)
                    continue;

                var read = DocumentWalker.Of(compilation, tree, cancellationToken);

                facts.Usage.Add(read, project.Name, file);
                facts.Graph.Add(read);
                facts._namespaces[document.Id] = read.Namespaces;
                facts._registrations.AddRange(read.Registrations.Select(registration => (file, registration)));
            }

            foreach (var tree in compilation.SyntaxTrees)
            {
                if (markup.TryGetValue(tree.FilePath, out var authored))
                {
                    facts.Usage.Add(
                        DocumentWalker.Generated(compilation, tree, authored, cancellationToken),
                        project.Name,
                        authored);
                }
            }
        }

        return facts;
    }

    /// <summary>The namespaces a document declares, in the order it declares them.</summary>
    public IReadOnlyList<DeclaredNamespace> NamespacesIn(DocumentId document) =>
        _namespaces.GetValueOrDefault(document, []);
}
