using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Turns the difference between two solution snapshots into edits the caller can apply.</summary>
internal sealed class SolutionEdits(IPathMap paths)
{
    private const int ConflictCap = 20;

    public async Task<IReadOnlyList<FileEdit>> BetweenAsync(
        Solution before,
        Solution after,
        CancellationToken cancellationToken)
    {
        List<FileEdit> edits = [];

        foreach (var projectChange in after.GetChanges(before).GetProjectChanges())
        {
            foreach (var documentId in projectChange.GetChangedDocuments())
            {
                if (before.GetDocument(documentId) is not { } oldDocument
                    || after.GetDocument(documentId) is not { } newDocument
                    || oldDocument.FilePath is not { } path)
                    continue;

                var oldText = await oldDocument.GetTextAsync(cancellationToken);
                var newText = await newDocument.GetTextAsync(cancellationToken);
                var changes = await newDocument.GetTextChangesAsync(oldDocument, cancellationToken);

                edits.Add(Describe(paths.ToCaller(path), RenamedTo(oldDocument, newDocument), oldText, newText, changes));
            }

            edits.AddRange(await MovedAsync(projectChange, cancellationToken));
        }

        return edits;
    }

    public IReadOnlyList<FileEdit> WithFileRename(IReadOnlyList<FileEdit> edits, string declaring, string newName)
    {
        if (Path.GetDirectoryName(declaring) is not { } folder)
            return edits;

        var from = paths.ToCaller(declaring);
        var to = paths.ToCaller(Path.Combine(folder, $"{newName}{Path.GetExtension(declaring)}"));

        List<FileEdit> merged = [.. edits];
        var index = merged.FindIndex(edit => string.Equals(edit.File, from, StringComparison.Ordinal));

        if (index >= 0)
            merged[index] = merged[index] with { RenamedTo = to };
        else
            merged.Add(new FileEdit(from, to, []));

        return merged;
    }

    /// <summary>Keys of everything an edit already covers: one per changed line, or the bare path when the whole file is replaced.</summary>
    public static HashSet<string> Touched(IReadOnlyList<FileEdit> edits) =>
    [
        .. edits.SelectMany(edit => edit.Content is null
            ? edit.Lines.Select(line => $"{edit.File}:{line.Line}")
            : [edit.File])
    ];

    public static async Task<IReadOnlyList<string>> ConflictsAsync(
        Solution before,
        Solution after,
        CancellationToken cancellationToken)
    {
        List<string> conflicts = [];

        foreach (var projectChange in after.GetChanges(before).GetProjectChanges())
        {
            var wasErrors = await ErrorsAsync(projectChange.OldProject, cancellationToken);
            var nowErrors = await ErrorsAsync(projectChange.NewProject, cancellationToken);

            conflicts.AddRange(nowErrors.Except(wasErrors, StringComparer.Ordinal));
        }

        return [.. conflicts.Distinct(StringComparer.Ordinal).Take(ConflictCap)];
    }

    public static async Task<HashSet<string>> ErrorsAsync(Project project, CancellationToken cancellationToken)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken);

        if (compilation is null)
            return [];

        return
        [
            .. compilation.GetDiagnostics(cancellationToken)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                .Select(diagnostic => $"{diagnostic.Id}: {diagnostic.GetMessage()}")
        ];
    }

    private async Task<IReadOnlyList<FileEdit>> MovedAsync(ProjectChanges change, CancellationToken cancellationToken)
    {
        List<Document> removed = [.. change.GetRemovedDocuments().Select(change.OldProject.GetDocument).OfType<Document>()];
        List<Document> added = [.. change.GetAddedDocuments().Select(change.NewProject.GetDocument).OfType<Document>()];

        if (removed.Count != added.Count)
            return [];

        List<FileEdit> moves = [];

        foreach (var (gone, arrived) in removed.Zip(added))
        {
            if (gone.FilePath is not { } from || arrived.FilePath is not { } to)
                continue;

            var oldText = await gone.GetTextAsync(cancellationToken);
            var newText = await arrived.GetTextAsync(cancellationToken);
            var changes = await arrived.GetTextChangesAsync(gone, cancellationToken);

            moves.Add(Describe(paths.ToCaller(from), paths.ToCaller(to), oldText, newText, changes));
        }

        return moves;
    }

    private string? RenamedTo(Document before, Document after) =>
        before.FilePath is { } from && after.FilePath is { } to && !string.Equals(from, to, StringComparison.Ordinal)
            ? paths.ToCaller(to)
            : null;

    /// <summary>Prefers line edits, and falls back to the whole file when the change moved lines around.</summary>
    private static FileEdit Describe(
        string file,
        string? renamedTo,
        SourceText before,
        SourceText after,
        IEnumerable<TextChange> changes)
    {
        if (before.ContentEquals(after))
            return new FileEdit(file, renamedTo, []);

        var lines = Lines(before, after, changes);

        return lines.Count > 0
            ? new FileEdit(file, renamedTo, lines)
            : new FileEdit(file, renamedTo, [], after.ToString());
    }

    private static IReadOnlyList<LineEdit> Lines(SourceText before, SourceText after, IEnumerable<TextChange> changes)
    {
        if (before.Lines.Count != after.Lines.Count)
            return [];

        List<LineEdit> lines = [];

        foreach (var number in changes
                     .Select(change => before.Lines.GetLineFromPosition(change.Span.Start).LineNumber)
                     .Distinct()
                     .Order())
        {
            var was = before.Lines[number].ToString();
            var now = after.Lines[number].ToString();

            if (!string.Equals(was, now, StringComparison.Ordinal))
                lines.Add(new LineEdit(number + 1, was, now));
        }

        return lines;
    }
}
