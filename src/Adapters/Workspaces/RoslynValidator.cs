using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Compiles a file's proposed contents in the context of the solution, before it reaches the disk.</summary>
internal sealed class RoslynValidator(IPathMap paths, SolutionCache cache) : ICodeValidator
{
    private const string NewFileNote =
        "This path holds no document yet, so it was compiled as a new file of the nearest project. "
        + "Adding a file for real also changes what the workspace contains: reload after writing it.";

    private const string CleanNote =
        "The proposed contents compile against the solution as it stands on disk right now.";

    public async Task<ValidationResult> ValidateAsync(
        string solutionId,
        string file,
        string content,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var path = paths.ToProcess(file);
        var documentId = solution.GetDocumentIdsWithFilePath(path).FirstOrDefault();

        if (documentId is not null && solution.GetDocument(documentId) is { } existing)
        {
            var after = solution.WithDocumentText(documentId, SourceText.From(content));

            return await CompareAsync(existing.Project, after.GetProject(existing.Project.Id)!, path, [], cancellationToken);
        }

        if (Nearest(solution, path) is not { } project)
            return new ValidationResult(false, paths.ToCaller(path), [], [],
                ["No project in the loaded solution owns this path, so there is no context to compile against."]);

        var created = DocumentId.CreateNewId(project.Id);
        var added = solution
            .AddDocument(created, Path.GetFileName(path), SourceText.From(content), filePath: path)
            .GetProject(project.Id)!;

        return await CompareAsync(project, added, path, [NewFileNote], cancellationToken);
    }

    private async Task<ValidationResult> CompareAsync(
        Project before,
        Project after,
        string path,
        IReadOnlyList<string> notes,
        CancellationToken cancellationToken)
    {
        var was = await IssuesAsync(before, cancellationToken);
        var now = await IssuesAsync(after, cancellationToken);

        List<CodeIssue> introduced = [.. now.Where(issue => !was.ContainsKey(issue.Key)).Select(issue => issue.Value)];
        List<CodeIssue> resolved = [.. was.Where(issue => !now.ContainsKey(issue.Key)).Select(issue => issue.Value)];

        List<string> all = [.. notes];

        if (introduced.Count == 0)
            all.Add(CleanNote);

        return new ValidationResult(
            introduced.All(issue => issue.Severity != IssueSeverity.Error),
            paths.ToCaller(path),
            introduced,
            resolved,
            all);
    }

    private async Task<Dictionary<string, CodeIssue>> IssuesAsync(Project project, CancellationToken cancellationToken)
    {
        var compilation = await project.GetCompilationAsync(cancellationToken);

        if (compilation is null)
            return [];

        Dictionary<string, CodeIssue> issues = [];

        foreach (var diagnostic in compilation.GetDiagnostics(cancellationToken))
        {
            if (diagnostic.Severity is not (DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
                continue;

            var span = diagnostic.Location.GetMappedLineSpan();
            var source = SourcePaths.Of(diagnostic);

            var issue = new CodeIssue(
                diagnostic.Id,
                diagnostic.Severity == DiagnosticSeverity.Error ? IssueSeverity.Error : IssueSeverity.Warning,
                diagnostic.GetMessage(),
                source is null ? null : paths.ToCaller(source),
                span.StartLinePosition.Line + 1,
                span.StartLinePosition.Character + 1,
                1);

            issues[Key(issue)] = issue;
        }

        return issues;
    }

    private static string Key(CodeIssue issue) => $"{issue.Id}|{issue.File}|{issue.Message}";

    private static Project? Nearest(Solution solution, string path) =>
        solution.Projects
            .Where(project => project.Language == LanguageNames.CSharp && project.FilePath is not null)
            .Where(project => path.StartsWith(Path.GetDirectoryName(project.FilePath!)!, StringComparison.Ordinal))
            .MaxBy(project => Path.GetDirectoryName(project.FilePath!)!.Length);
}
