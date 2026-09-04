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

    /// <summary>The syntax tree the Razor generator produced from one markup file, or null when it did not run.</summary>
    /// <remarks>Matched on the generator's own hint name. A suffix match on the file path instead lets a root
    /// component claim the output of a nested one with the same leaf name, and which of the two it gets depends
    /// on the order the compilation happens to list its trees in.</remarks>
    public static async Task<SyntaxTree?> FindGeneratedAsync(
        Project project,
        TextDocument markup,
        CancellationToken cancellationToken)
    {
        var hint = Normalize(ExpectedHintName(markup));

        foreach (var generated in await project.GetSourceGeneratedDocumentsAsync(cancellationToken))
        {
            if (Normalize(generated.HintName).Equals(hint, StringComparison.OrdinalIgnoreCase))
                return await generated.GetSyntaxTreeAsync(cancellationToken);
        }

        return null;
    }

    /// <summary>Generated document path to the markup file it was produced from, across the solution.</summary>
    public static async Task<IReadOnlyDictionary<string, string>> BuildAsync(
        Solution solution,
        CancellationToken cancellationToken)
    {
        Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);

        foreach (var project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
        {
            Dictionary<string, string> byHint = new(StringComparer.OrdinalIgnoreCase);

            foreach (var document in project.AdditionalDocuments)
            {
                if (IsMarkup(document.FilePath) && document.FilePath is { } authored)
                    byHint[Normalize(ExpectedHintName(document))] = authored;
            }

            if (byHint.Count == 0)
                continue;

            foreach (var generated in await project.GetSourceGeneratedDocumentsAsync(cancellationToken))
            {
                if (generated.FilePath is { } file && byHint.TryGetValue(Normalize(generated.HintName), out var authored))
                    map[file] = authored;
            }
        }

        return map;
    }

    private static string Normalize(string path) => path.Replace(Path.DirectorySeparatorChar, '/');
}
