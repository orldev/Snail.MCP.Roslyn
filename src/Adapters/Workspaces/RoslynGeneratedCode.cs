using Microsoft.CodeAnalysis;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Shows what the generators contributed, so a symbol with no declaration in source can be traced to its origin.</summary>
internal sealed class RoslynGeneratedCode(IPathMap paths, SolutionCache cache) : IGeneratedCodeReader
{
    private const string EditNote =
        "Generated files are rebuilt on every compile: editing them changes nothing. "
        + "Change the input the generator reads — a .razor file, a partial declaration, an attribute — not the output.";

    private const string EmptyNote =
        "No generated documents were produced for this project. "
        + "Razor output is written by the build into obj/ instead, and appears here only after a build.";

    public async Task<GeneratedCodePage> ReadAsync(
        string solutionId,
        string? project,
        string? hintName,
        bool includeText,
        int limit,
        CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var target = Select(solution, project);

        if (target is null)
            return new GeneratedCodePage(project ?? string.Empty, [], [], 0, false,
                ["No such project in the loaded solution. Call load_solution to see the project names."]);

        var cap = Math.Clamp(limit, 1, 100);
        var generators = Generators(target);
        var documents = await target.GetSourceGeneratedDocumentsAsync(cancellationToken);

        List<GeneratedFile> files = [];
        var total = 0;

        foreach (var document in documents)
        {
            if (hintName is not null && !document.HintName.Contains(hintName, StringComparison.OrdinalIgnoreCase))
                continue;

            total++;

            if (files.Count >= cap)
                continue;

            var text = await document.GetTextAsync(cancellationToken);

            files.Add(new GeneratedFile(
                "source generator",
                document.HintName,
                text.Lines.Count,
                includeText ? text.ToString() : null));
        }

        files.AddRange(FromBuildOutput(target, hintName, includeText, cap - files.Count, ref total));

        return new GeneratedCodePage(
            target.Name,
            generators,
            files,
            total,
            total > files.Count,
            total == 0 ? [EmptyNote] : [EditNote]);
    }

    private static Project? Select(Solution solution, string? name)
    {
        var candidates = solution.Projects.Where(project => project.Language == LanguageNames.CSharp).ToList();

        if (string.IsNullOrWhiteSpace(name))
            return candidates.FirstOrDefault();

        return candidates.FirstOrDefault(project => project.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> Generators(Project project) =>
    [
        .. project.AnalyzerReferences
            .SelectMany(reference => reference.GetGenerators(LanguageNames.CSharp))
            .Select(generator => generator.GetType().Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
    ];

    private List<GeneratedFile> FromBuildOutput(Project project, string? hintName, bool includeText, int room, ref int total)
    {
        List<GeneratedFile> files = [];

        if (project.FilePath is not { } file || Path.GetDirectoryName(file) is not { } root)
            return files;

        var generated = Path.Combine(root, "obj");

        if (!Directory.Exists(generated))
            return files;

        foreach (var candidate in Directory.EnumerateFiles(generated, "*.g.cs", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(candidate);

            if (hintName is not null && !name.Contains(hintName, StringComparison.OrdinalIgnoreCase))
                continue;

            total++;

            if (files.Count >= room)
                continue;

            var text = File.ReadAllText(candidate);

            files.Add(new GeneratedFile(
                "build output",
                paths.ToCaller(candidate),
                text.AsSpan().Count('\n') + 1,
                includeText ? text : null));
        }

        return files;
    }
}
