using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Maps generator output back to the .razor or .cshtml file the author actually wrote.</summary>
/// <remarks>
/// Hint-name matching adapted from roslyn-codelens-mcp (MIT, © Marcel Roozekrans).
/// Line numbers are deliberately not mapped: the Razor generator emits #line only for user-written C#,
/// so a component tag has no authored line to recover, and inventing one would be worse than omitting it.
/// </remarks>
internal static class MarkupMap
{
    private static readonly string[] Extensions = [".razor", ".cshtml"];

    public static bool IsMarkup(string? path) =>
        path is not null && Extensions.Any(extension => path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    /// <summary>Components/Pages/Counter.razor becomes Components/Pages/Counter_razor.g.cs.</summary>
    public static string ExpectedHintName(TextDocument markup)
    {
        var file = $"{markup.Name.Replace('.', '_')}.g.cs";

        return markup.Folders.Count == 0 ? file : $"{string.Join('/', markup.Folders)}/{file}";
    }

    /// <summary>Where the author wrote this declaration, judged by its identifier: the generator maps the name of an
    /// @inject property while its synthesized attribute stays unmapped, so the identifier is the reliable anchor.</summary>
    public static FileLinePositionSpan AuthoredSpan(SyntaxNode declaration)
    {
        var identifier = declaration switch
        {
            BaseTypeDeclarationSyntax type => type.Identifier,
            DelegateDeclarationSyntax @delegate => @delegate.Identifier,
            MethodDeclarationSyntax method => method.Identifier,
            PropertyDeclarationSyntax property => property.Identifier,
            EventDeclarationSyntax @event => @event.Identifier,
            VariableDeclaratorSyntax variable => variable.Identifier,
            _ => default
        };

        var location = identifier == default ? declaration.GetLocation() : identifier.GetLocation();

        return location.GetMappedLineSpan();
    }

    /// <summary>The syntax tree the Razor generator produced from one markup file, or null when the generator did not run.</summary>
    public static SyntaxTree? FindGenerated(Compilation compilation, TextDocument markup)
    {
        var hint = Normalize(ExpectedHintName(markup));

        return compilation.SyntaxTrees.FirstOrDefault(tree =>
            tree.FilePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            && Normalize(tree.FilePath).EndsWith(hint, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Generated document path to the markup file it was produced from, across the solution.</summary>
    public static async Task<IReadOnlyDictionary<string, string>> BuildAsync(
        Solution solution,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);

        foreach (var project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
        {
            List<TextDocument> markup = [.. project.AdditionalDocuments.Where(document => IsMarkup(document.FilePath))];

            if (markup.Count == 0)
                continue;

            var compilation = await project.GetCompilationAsync(cancellationToken);

            if (compilation is null)
                continue;

            List<string> generated =
            [
                .. compilation.SyntaxTrees
                    .Select(tree => tree.FilePath)
                    .Where(path => !string.IsNullOrEmpty(path) && path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
            ];

            foreach (var document in markup)
                Connect(document, generated, map);
        }

        return map;
    }

    private static void Connect(TextDocument markup, List<string> generated, Dictionary<string, string> map)
    {
        if (markup.FilePath is null)
            return;

        var hint = Normalize(ExpectedHintName(markup));

        foreach (var path in generated)
        {
            if (!Normalize(path).EndsWith(hint, StringComparison.OrdinalIgnoreCase))
                continue;

            map[path] = markup.FilePath;

            return;
        }
    }

    private static string Normalize(string path) => path.Replace(Path.DirectorySeparatorChar, '/');
}
