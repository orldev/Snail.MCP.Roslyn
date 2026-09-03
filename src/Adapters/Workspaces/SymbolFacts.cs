using Microsoft.CodeAnalysis;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Renders compiler symbols into the shapes the caller receives.</summary>
internal static class SymbolFacts
{
    private static readonly SymbolDisplayFormat Signature = SymbolDisplayFormat.CSharpErrorMessageFormat;

    public static string IdOf(ISymbol symbol) =>
        DocumentationCommentId.CreateDeclarationId(symbol) ?? symbol.ToDisplayString();

    public static SymbolMatch Match(ISymbol symbol, IPathMap paths)
    {
        var (file, line) = Where(symbol, paths);

        return new SymbolMatch(
            IdOf(symbol),
            symbol.Name,
            symbol.Kind.ToString(),
            symbol.ContainingType?.ToDisplayString() ?? symbol.ContainingNamespace?.ToDisplayString(),
            symbol.DeclaredAccessibility.ToString(),
            file,
            line,
            IsGenerated(symbol, file));
    }

    public static bool IsGenerated(ISymbol symbol, string? file) =>
        file is null || SourcePaths.IsUnderOutput(file) || !symbol.Locations.Any(location => location.IsInSource);

    public static SymbolDetails Details(ISymbol symbol, IPathMap paths)
    {
        var (file, line) = Where(symbol, paths);
        var documentation = symbol.GetDocumentationCommentXml();

        return new SymbolDetails(
            IdOf(symbol),
            symbol.Name,
            symbol.Kind.ToString(),
            symbol.ToDisplayString(Signature),
            symbol.DeclaredAccessibility.ToString(),
            symbol.ContainingType?.ToDisplayString(),
            symbol.ContainingNamespace?.ToDisplayString(),
            file,
            line,
            string.IsNullOrWhiteSpace(documentation) ? null : documentation.Trim(),
            file is not null && SourcePaths.IsUnderOutput(file));
    }

    public static bool Matches(ISymbol symbol, SymbolKindFilter filter) => filter switch
    {
        SymbolKindFilter.Any => true,
        SymbolKindFilter.Type => symbol.Kind == SymbolKind.NamedType,
        SymbolKindFilter.Method => symbol.Kind == SymbolKind.Method,
        SymbolKindFilter.Property => symbol.Kind == SymbolKind.Property,
        SymbolKindFilter.Field => symbol.Kind == SymbolKind.Field,
        SymbolKindFilter.Event => symbol.Kind == SymbolKind.Event,
        _ => true
    };

    private static (string? File, int Line) Where(ISymbol symbol, IPathMap paths)
    {
        var location = symbol.Locations.FirstOrDefault(candidate => candidate.IsInSource);

        if (location is null)
            return (null, 0);

        var span = location.GetMappedLineSpan();

        return (string.IsNullOrEmpty(span.Path) ? null : paths.ToCaller(span.Path), span.StartLinePosition.Line + 1);
    }
}
