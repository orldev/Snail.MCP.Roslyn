using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Names what a reference does, so fifty results can be judged without reading fifty files.</summary>
/// <remarks>Kind vocabulary adapted from roslyn-codelens-mcp (MIT, © Marcel Roozekrans).</remarks>
internal static class ReferenceKinds
{
    public static string Classify(SyntaxNode node, ISymbol symbol)
    {
        var name = node as SimpleNameSyntax ?? node.FirstAncestorOrSelf<SimpleNameSyntax>();

        if (name is null)
            return node.FirstAncestorOrSelf<ConstructorInitializerSyntax>() is not null ? "invocation" : "usage";

        if (InsideNameof(name))
            return "nameof";

        if (name.FirstAncestorOrSelf<XmlCrefAttributeSyntax>() is not null || name.FirstAncestorOrSelf<CrefSyntax>() is not null)
            return "documentation";

        if (name.FirstAncestorOrSelf<AttributeSyntax>() is { } attribute && attribute.Name.Span.Contains(name.Span))
            return "attribute";

        return TypeLike(symbol) ? ClassifyType(name, symbol) : ClassifyMember(name, symbol);
    }

    private static bool TypeLike(ISymbol symbol) =>
        symbol is INamedTypeSymbol or ITypeParameterSymbol or INamespaceSymbol
            or IMethodSymbol { MethodKind: MethodKind.Constructor };

    private static string ClassifyType(SimpleNameSyntax name, ISymbol symbol)
    {
        if (name.FirstAncestorOrSelf<ObjectCreationExpressionSyntax>() is { Type: { } created } && created.Span.Contains(name.Span))
            return "object_creation";

        if (name.FirstAncestorOrSelf<TypeOfExpressionSyntax>() is { Type: { } queried } && queried.Span.Contains(name.Span))
            return "typeof";

        if (name.FirstAncestorOrSelf<BaseTypeSyntax>() is not null)
            return "base_type";

        if (name.FirstAncestorOrSelf<TypeConstraintSyntax>() is not null)
            return "type_constraint";

        if (name.FirstAncestorOrSelf<TypeArgumentListSyntax>() is { } arguments && arguments.Span.Contains(name.Span))
            return "type_argument";

        if (name.FirstAncestorOrSelf<CastExpressionSyntax>() is { Type: { } cast } && cast.Span.Contains(name.Span))
            return "cast";

        if (name.FirstAncestorOrSelf<BinaryExpressionSyntax>() is { } binary
            && binary.IsKind(SyntaxKind.IsExpression) | binary.IsKind(SyntaxKind.AsExpression)
            && binary.Right.Span.Contains(name.Span))
            return "type_check";

        if (name.FirstAncestorOrSelf<IsPatternExpressionSyntax>() is { Pattern: { } pattern } && pattern.Span.Contains(name.Span))
            return "type_check";

        return symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } ? "object_creation" : "type_usage";
    }

    private static string ClassifyMember(SimpleNameSyntax name, ISymbol symbol)
    {
        if (symbol is IMethodSymbol)
            return Invoked(name) ? "invocation" : "method_group";

        if (symbol is not (IPropertySymbol or IFieldSymbol or IEventSymbol or ILocalSymbol or IParameterSymbol))
            return "usage";

        var target = Widest(name);

        if (target.FirstAncestorOrSelf<AssignmentExpressionSyntax>() is { Left: { } left } assignment
            && left.Span.Contains(target.Span))
            return assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) ? "write" : "readwrite";

        if (target.Parent is PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax)
            return "readwrite";

        return "read";
    }

    private static bool Invoked(SimpleNameSyntax name)
    {
        var expression = name.Parent is MemberAccessExpressionSyntax { Name: { } accessed } access && accessed == name
            ? (ExpressionSyntax)access
            : name;

        return expression.Parent is InvocationExpressionSyntax invocation && invocation.Expression == expression;
    }

    private static ExpressionSyntax Widest(SimpleNameSyntax name) =>
        name.Parent is MemberAccessExpressionSyntax { Name: { } accessed } access && accessed == name
            ? access
            : name;

    private static bool InsideNameof(SimpleNameSyntax name) =>
        name.FirstAncestorOrSelf<InvocationExpressionSyntax>(invocation =>
            invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" }) is not null;
}
