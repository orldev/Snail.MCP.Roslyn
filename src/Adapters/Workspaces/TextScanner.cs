using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Finds occurrences of a name that the compiler does not treat as references, so they are named rather than silently missed.</summary>
internal sealed class TextScanner(IPathMap paths)
{
    private const int Cap = 200;

    public async Task<IReadOnlyList<TextualMatch>> UnhandledAsync(
        Solution solution,
        string name,
        HashSet<string> touched,
        CancellationToken cancellationToken)
    {
        List<TextualMatch> found = [];
        HashSet<string> seen = [];

        foreach (var project in solution.Projects)
        {
            foreach (var document in project.Documents)
            {
                if (found.Count >= Cap)
                    return found;

                var text = await document.GetTextAsync(cancellationToken);
                var content = text.ToString();

                // The root is only needed to say what kind of occurrence this is. Asking for it before knowing
                // there is one parses every file in the solution to answer a question about a handful of them.
                if (!content.Contains(name, StringComparison.Ordinal))
                    continue;

                Scan(
                    document.FilePath,
                    text,
                    content,
                    await document.GetSyntaxRootAsync(cancellationToken),
                    name,
                    touched,
                    seen,
                    found);
            }

            foreach (var document in project.AdditionalDocuments)
            {
                if (found.Count >= Cap)
                    return found;

                var text = await document.GetTextAsync(cancellationToken);
                var content = text.ToString();

                if (content.Contains(name, StringComparison.Ordinal))
                    Scan(document.FilePath, text, content, null, name, touched, seen, found);
            }
        }

        return found;
    }

    public IReadOnlyList<TextualMatch> Declaring(Solution solution, string name)
    {
        List<TextualMatch> declaring = [];
        HashSet<string> seen = [];

        foreach (var project in solution.Projects)
        {
            if (project.FilePath is not { } projectFile || Path.GetDirectoryName(projectFile) is not { } root)
                continue;

            foreach (var candidate in Directory.EnumerateFiles(root, $"{name}.*", SearchOption.AllDirectories))
            {
                if (SourcePaths.IsUnderOutput(candidate) || Path.GetExtension(candidate) is not (".razor" or ".cs" or ".cshtml"))
                    continue;

                if (!seen.Add(candidate))
                    continue;

                declaring.Add(new TextualMatch(
                    paths.ToCaller(candidate),
                    1,
                    Path.GetFileName(candidate),
                    "source file whose name declares this symbol — rename the file itself"));
            }
        }

        return declaring;
    }

    private void Scan(
        string? path,
        SourceText text,
        string content,
        SyntaxNode? root,
        string name,
        HashSet<string> touched,
        HashSet<string> seen,
        List<TextualMatch> found)
    {
        if (path is null || SourcePaths.IsUnderOutput(path))
            return;

        var caller = paths.ToCaller(path);

        if (touched.Contains(caller))
            return;

        var index = content.IndexOf(name, StringComparison.Ordinal);

        while (index >= 0 && found.Count < Cap)
        {
            if (IsWholeWord(content, index, name.Length))
            {
                var line = text.Lines.GetLineFromPosition(index);
                var key = $"{caller}:{line.LineNumber + 1}";

                if (!touched.Contains(key) && seen.Add(key))
                    found.Add(new TextualMatch(caller, line.LineNumber + 1, line.ToString().Trim(), Classify(root, index)));
            }

            index = content.IndexOf(name, index + name.Length, StringComparison.Ordinal);
        }
    }

    private static bool IsWholeWord(string content, int index, int length)
    {
        var before = index == 0 || !IsWordCharacter(content[index - 1]);
        var after = index + length >= content.Length || !IsWordCharacter(content[index + length]);

        return before && after;
    }

    private static bool IsWordCharacter(char value) => char.IsLetterOrDigit(value) || value == '_';

    private static string Classify(SyntaxNode? root, int position)
    {
        if (root is null)
            return "markup or non-C# document";

        var trivia = root.FindTrivia(position);

        if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
            || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
            || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
            return "comment";

        var token = root.FindToken(position, findInsideTrivia: true);

        if (token.IsKind(SyntaxKind.StringLiteralToken) || token.IsKind(SyntaxKind.InterpolatedStringTextToken))
            return "string literal";

        return "unrelated symbol with the same name";
    }
}
