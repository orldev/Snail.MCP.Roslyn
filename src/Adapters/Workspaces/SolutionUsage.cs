using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Counts every use of every source declaration in a single pass, so solution-wide questions cost one walk.</summary>
internal sealed class SolutionUsage
{
    private readonly Dictionary<string, UsageEntry> entries = new(StringComparer.Ordinal);

    private SolutionUsage()
    {
    }

    public IReadOnlyCollection<UsageEntry> Entries => entries.Values;

    public static async Task<SolutionUsage> BuildAsync(Solution solution, CancellationToken cancellationToken)
    {
        var index = new SolutionUsage();
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

                await index.WalkAsync(document, project.Name, file, cancellationToken);
            }

            foreach (var tree in compilation.SyntaxTrees)
            {
                if (markup.TryGetValue(tree.FilePath, out var authored))
                    index.WalkGenerated(compilation.GetSemanticModel(tree), tree, project.Name, authored, cancellationToken);
            }
        }

        return index;
    }

    public UsageEntry? For(ISymbol symbol) =>
        entries.GetValueOrDefault(SymbolFacts.IdOf(symbol.OriginalDefinition));

    public int CountFor(ISymbol symbol) => For(symbol)?.References ?? 0;

    private async Task WalkAsync(Document document, string project, string file, CancellationToken cancellationToken)
    {
        var model = await document.GetSemanticModelAsync(cancellationToken);
        var root = await document.GetSyntaxRootAsync(cancellationToken);

        if (model is null || root is null)
            return;

        foreach (var declaration in root.DescendantNodes().Where(IsDeclaration))
        {
            if (model.GetDeclaredSymbol(declaration, cancellationToken) is { } declared)
                Declare(declared);
        }

        foreach (var name in root.DescendantNodes().OfType<SimpleNameSyntax>())
        {
            var symbol = model.GetSymbolInfo(name, cancellationToken).Symbol;

            if (symbol is null)
                continue;

            Count(symbol, project, file);
        }
    }

    /// <summary>Counts uses inside one generator-produced tree, attributed to the markup file the author wrote,
    /// and registers the declarations whose identifiers map back into that file, such as @code members.</summary>
    private void WalkGenerated(
        SemanticModel model,
        SyntaxTree tree,
        string project,
        string authored,
        CancellationToken cancellationToken)
    {
        var root = tree.GetRoot(cancellationToken);

        foreach (var declaration in root.DescendantNodes().Where(IsDeclaration))
        {
            if (model.GetDeclaredSymbol(declaration, cancellationToken) is not { } declared)
                continue;

            var span = MarkupMap.AuthoredSpan(declaration);

            if (span.HasMappedPath && string.Equals(span.Path, authored, StringComparison.OrdinalIgnoreCase))
                Declare(declared);
        }

        foreach (var name in root.DescendantNodes().OfType<SimpleNameSyntax>())
        {
            var symbol = model.GetSymbolInfo(name, cancellationToken).Symbol;

            if (symbol is null)
                continue;

            Count(symbol, project, authored);
        }
    }

    private static bool IsDeclaration(SyntaxNode node) =>
        node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax or MethodDeclarationSyntax
            or PropertyDeclarationSyntax or EventDeclarationSyntax or VariableDeclaratorSyntax;

    private void Declare(ISymbol symbol)
    {
        if (symbol.Kind is not (SymbolKind.NamedType or SymbolKind.Method or SymbolKind.Property
            or SymbolKind.Field or SymbolKind.Event))
            return;

        Entry(symbol.OriginalDefinition);
    }

    private void Count(ISymbol symbol, string project, string file)
    {
        var target = Normalize(symbol);

        if (target is null || !target.Locations.Any(location => location.IsInSource))
            return;

        var entry = Entry(target);

        entry.References++;
        entry.Projects.Add(project);
        entry.Files.Add(file);

        if (target is IMethodSymbol { MethodKind: MethodKind.Constructor, ContainingType: { } owner })
            Count(owner, project, file);
    }

    private static ISymbol? Normalize(ISymbol symbol)
    {
        var target = symbol is IMethodSymbol { ReducedFrom: { } reduced } ? reduced : symbol;

        return target.Kind is SymbolKind.NamedType or SymbolKind.Method or SymbolKind.Property
            or SymbolKind.Field or SymbolKind.Event
            ? target.OriginalDefinition
            : null;
    }

    private UsageEntry Entry(ISymbol symbol)
    {
        var id = SymbolFacts.IdOf(symbol);

        if (entries.TryGetValue(id, out var existing))
            return existing;

        var created = new UsageEntry(symbol);
        entries[id] = created;

        return created;
    }
}
