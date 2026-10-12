using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Reads one syntax tree once for every index that would otherwise walk it on its own.</summary>
/// <remarks>Facts are kept per compilation, not per tree: an unchanged tree binds differently under a changed
/// compilation — renaming a type in one file changes what the names in another file resolve to — so only the
/// compilation that produced them may reuse them, and an edit costs a walk of its own project alone.</remarks>
internal static class DocumentWalker
{
    private static readonly ConditionalWeakTable<Compilation, ConditionalWeakTable<SyntaxTree, DocumentFacts>> Read = [];

    public static DocumentFacts Of(Compilation compilation, SyntaxTree tree, CancellationToken cancellationToken) =>
        Facts(compilation, tree, null, cancellationToken);

    /// <summary>Facts of a generator-produced tree: only the declarations mapping back into the markup file the
    /// author wrote are its own, and the namespaces it declares are the generator's invention, not the author's.</summary>
    public static DocumentFacts Generated(
        Compilation compilation,
        SyntaxTree tree,
        string authored,
        CancellationToken cancellationToken) =>
        Facts(compilation, tree, authored, cancellationToken);

    private static DocumentFacts Facts(
        Compilation compilation,
        SyntaxTree tree,
        string? authored,
        CancellationToken cancellationToken)
    {
        var known = Read.GetValue(compilation, _ => []);

        if (known.TryGetValue(tree, out var cached))
            return cached;

        var pass = new Pass(compilation.GetSemanticModel(tree), authored, cancellationToken);
        pass.Visit(tree.GetRoot(cancellationToken));

        return known.GetValue(tree, _ => pass.Facts());
    }

    private sealed class Pass(SemanticModel model, string? authored, CancellationToken cancellationToken)
        : CSharpSyntaxWalker
    {
        private readonly List<ISymbol> _declarations = [];

        private readonly Dictionary<ISymbol, int> _uses = new(SymbolEqualityComparer.Default);

        private readonly HashSet<NamespaceEdge> _edges = [];

        private readonly List<DeclaredNamespace> _namespaces = [];

        private readonly List<RegistrationFact> _registrations = [];

        private readonly Stack<(ISymbol? Type, string Namespace)> _containers = new();

        public DocumentFacts Facts() =>
            new(
                _declarations,
                [.. _uses.Select(use => new SymbolUse(use.Key, use.Value))],
                [.. _edges],
                _namespaces,
                _registrations);

        public override void Visit(SyntaxNode? node)
        {
            if (node is null)
                return;

            switch (node)
            {
                case BaseTypeDeclarationSyntax type:
                    Enter(type);
                    return;
                case BaseNamespaceDeclarationSyntax declaration when authored is null:
                    _namespaces.Add(new DeclaredNamespace(declaration.Name.ToString(), Line(declaration.Name)));
                    break;
                case SimpleNameSyntax name:
                    Use(name);
                    break;
                case InvocationExpressionSyntax call when authored is null:
                    if (RoslynContainer.Registered(model, call, cancellationToken) is { } registration)
                        _registrations.Add(registration);

                    break;
                default:
                    if (IsDeclaration(node) && model.GetDeclaredSymbol(node, cancellationToken) is { } declared)
                        Declare(node, declared);

                    break;
            }

            base.Visit(node);
        }

        private void Enter(BaseTypeDeclarationSyntax type)
        {
            var declared = model.GetDeclaredSymbol(type, cancellationToken);

            if (declared is not null)
                Declare(type, declared);

            _containers.Push(declared?.ContainingNamespace is { } owner
                ? (declared, owner.ToDisplayString())
                : (null, string.Empty));

            base.Visit(type);
            _containers.Pop();
        }

        private void Declare(SyntaxNode declaration, ISymbol declared)
        {
            if (authored is not null && !IsAuthored(declaration))
                return;

            if (SolutionUsage.Declares(declared) is { } target)
                _declarations.Add(target);
        }

        private void Use(SimpleNameSyntax name)
        {
            if (model.GetSymbolInfo(name, cancellationToken).Symbol is not { } symbol)
                return;

            if (authored is null
                && _containers.TryPeek(out var container)
                && container.Type is not null
                && NamespaceGraph.Edge(container.Namespace, container.Type, symbol) is { } edge)
                _edges.Add(edge);

            Count(symbol);
        }

        private void Count(ISymbol symbol)
        {
            if (SolutionUsage.Counts(symbol) is not { } target)
                return;

            _uses[target] = _uses.TryGetValue(target, out var seen) ? seen + 1 : 1;

            if (target is IMethodSymbol { MethodKind: MethodKind.Constructor, ContainingType: { } owner })
                Count(owner);
        }

        private bool IsAuthored(SyntaxNode declaration)
        {
            var span = MarkupMap.AuthoredSpan(declaration);

            return span.HasMappedPath && string.Equals(span.Path, authored, StringComparison.OrdinalIgnoreCase);
        }

        private int Line(SyntaxNode node) =>
            model.SyntaxTree.GetLineSpan(node.Span, cancellationToken).StartLinePosition.Line + 1;

        private static bool IsDeclaration(SyntaxNode node) =>
            node is DelegateDeclarationSyntax or MethodDeclarationSyntax or PropertyDeclarationSyntax
                or EventDeclarationSyntax or VariableDeclaratorSyntax;
    }
}
