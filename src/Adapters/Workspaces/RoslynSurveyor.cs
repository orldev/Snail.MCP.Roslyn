using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Options;
using Snail.MCP.Roslyn.Configuration;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

internal sealed class RoslynSurveyor(
    IPathMap paths,
    SolutionCache cache,
    ScriptTally scripts,
    IOptions<WorkspaceOptions> options) : ISolutionSurveyor
{
    private const string FolderRule = "folder-namespace";

    private const string CycleRule = "namespace-cycle";

    private const string ProjectCycleRule = "project-cycle";

    private const string ClaimNote =
        "Every finding is a claim about the code that can be checked at the stated file and line. "
        + "Whether it matters is a judgement this server does not make.";

    private const string DuplicationNote =
        "A markup-duplication finding compares lines with attribute values and razor expressions masked: "
        + "the named blocks render the same structure with different data. Whether the repetition deserves "
        + "a component is design judgment.";

    private const string HotspotNote =
        "Hotspots are the types the rest of the solution names most often. "
        + "They are where a change costs the most, not where the code is worst.";

    public async Task<SolutionOverview> OverviewAsync(
        string solutionId,
        int limit,
        CancellationToken cancellationToken,
        IProgress<WorkStep>? progress = null)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var cap = Math.Clamp(limit, 1, options.Value.MaxFindings);

        List<ProjectNode> graph = [];
        List<CodeIssue> issues = [];
        var documents = 0;
        var types = 0;
        var errors = 0;
        var warnings = 0;

        List<Project> surveyed = [.. solution.Projects.Where(IsCSharp)];
        var done = 0;

        foreach (var project in surveyed)
        {
            progress?.Report(new WorkStep(done++, surveyed.Count + 1, $"Compiling {project.Name}"));

            var compilation = await project.GetCompilationAsync(cancellationToken);
            var diagnostics = compilation?.GetDiagnostics(cancellationToken) ?? [];
            var projectErrors = diagnostics.Count(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

            documents += project.DocumentIds.Count;
            errors += projectErrors;
            warnings += diagnostics.Count(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning);

            var declared = compilation is null ? 0 : Count(compilation);
            types += declared;

            graph.Add(new ProjectNode(
                project.Name,
                TargetOf(project),
                project.DocumentIds.Count,
                declared,
                projectErrors,
                [.. project.ProjectReferences
                    .Select(reference => solution.GetProject(reference.ProjectId)?.Name)
                    .OfType<string>()
                    .Order(StringComparer.Ordinal)]));

            issues.AddRange(Top(diagnostics, cap));
        }

        progress?.Report(new WorkStep(done, surveyed.Count + 1, "Reading the solution as a whole"));

        var facts = await SolutionInsights.FactsAsync(solution, cancellationToken);
        var architecture = await FindingsAsync(solution, cap, cancellationToken);

        return new SolutionOverview(
            solutionId,
            graph.Count,
            documents,
            types,
            errors,
            warnings,
            [.. graph.OrderBy(project => project.Name, StringComparer.Ordinal)],
            ProjectCycles(solution),
            Hotspots(facts.Usage, cap),
            [.. issues.Take(cap)],
            [.. architecture.Take(cap)],
            [ClaimNote, HotspotNote]);
    }

    public async Task<ArchitectureReport> CheckAsync(string solutionId, int limit, CancellationToken cancellationToken)
    {
        var solution = await cache.FreshAsync(solutionId, cancellationToken);
        var cap = Math.Clamp(limit, 1, options.Value.MaxFindings);
        var findings = await FindingsAsync(solution, cap, cancellationToken);

        List<string> notes = [ClaimNote];

        if (findings.Any(finding => finding.Rule == MarkupDuplication.Rule))
            notes.Add(DuplicationNote);

        return new ArchitectureReport([.. findings.Take(cap)], findings.Count, findings.Count > cap, notes);
    }

    public async Task<HealthReport> HealthAsync(string solutionId, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var solution = await cache.FreshAsync(solutionId, cancellationToken);

        var projects = 0;
        var documents = 0;
        var compiled = true;
        var restored = true;

        foreach (var project in solution.Projects.Where(IsCSharp))
        {
            projects++;
            documents += project.DocumentIds.Count;
            restored &= project.MetadataReferences.Count > 0 && ProjectRestore.HasRun(project);

            var compilation = await project.GetCompilationAsync(cancellationToken);

            if (compilation is null)
                compiled = false;
            else
                compilation.GetDiagnostics(cancellationToken);
        }

        var working = ProcessFootprint.WorkingSetMegabytes;

        return new HealthReport(
            solutionId,
            Decide(projects, compiled, restored),
            projects,
            documents,
            (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            working,
            ProcessFootprint.ManagedHeapMegabytes,
            ProcessFootprint.Assemblies,
            ProcessFootprint.Foreign,
            scripts.Ran,
            cache.Loaded,
            options.Value.AllowScripts,
            Advice(compiled, restored, working, scripts.Ran, options.Value.MemoryAdviceMegabytes));
    }

    private static WorkspaceReadiness Decide(int projects, bool compiled, bool restored) =>
        projects == 0 || !compiled ? WorkspaceReadiness.Failed
        : restored ? WorkspaceReadiness.Ready
        : WorkspaceReadiness.Degraded;

    private static IReadOnlyList<string> Advice(
        bool compiled,
        bool restored,
        long working,
        int scripts,
        int ceiling)
    {
        List<string> notes = [];

        if (!compiled)
            notes.Add("At least one project produced no compilation: every semantic answer would be empty regardless of the code.");

        if (!restored)
            notes.Add(
                "Package restore has not run for at least one project: run 'dotnet restore' first. "
                + "Until then an error about a missing type is about the packages, not about the code, "
                + "and an empty semantic result does not mean the code is clean.");

        if (notes.Count == 0)
            notes.Add("The workspace answers from the files as they are on disk right now.");

        if (scripts > 0)
            notes.Add(
                $"{scripts} script(s) have run in this process. Each one permanently adds an assembly and some "
                + "megabytes of process memory that no collection returns, whether or not the code repeated one "
                + "already run. Measured on this server: about 7 MB per run.");

        if (working > ceiling)
            notes.Add(
                $"This process holds {working} MB, past the {ceiling} MB at which restarting is worth it. "
                + "Two of the things it holds are never given back: the assembly each run_script emits and the "
                + "analyzer assemblies the analysed solutions brought. Neither can be unloaded, so unload_solution "
                + "does not recover them — restart the server.");

        return notes;
    }

    private async Task<IReadOnlyList<ArchitectureFinding>> FindingsAsync(
        Solution solution,
        int cap,
        CancellationToken cancellationToken)
    {
        var facts = await SolutionInsights.FactsAsync(solution, cancellationToken);

        List<ArchitectureFinding> findings = [];

        foreach (var project in solution.Projects.Where(IsCSharp))
        {
            foreach (var document in project.Documents)
                findings.AddRange(Mismatch(project, document, facts));
        }

        findings.AddRange(facts.Graph.Cycles()
            .Take(cap)
            .Select(cycle => new ArchitectureFinding(CycleRule, $"Namespaces depend on each other in a cycle: {cycle}.", null, 0)));

        findings.AddRange(ProjectCycles(solution)
            .Select(cycle => new ArchitectureFinding(ProjectCycleRule, $"Projects depend on each other in a cycle: {cycle}.", null, 0)));

        findings.AddRange(await MarkupDuplication.FindAsync(solution, paths, cap, cancellationToken));

        return findings;
    }

    private IReadOnlyList<ArchitectureFinding> Mismatch(Project project, Document document, SolutionFacts facts)
    {
        if (document.FilePath is not { } file || SourcePaths.IsUnderOutput(file))
            return [];

        var expected = Expected(project, document);
        List<ArchitectureFinding> findings = [];

        foreach (var (declared, line) in facts.NamespacesIn(document.Id))
        {
            if (string.Equals(declared, expected, StringComparison.Ordinal))
                continue;

            findings.Add(new ArchitectureFinding(
                FolderRule,
                $"Namespace '{declared}' does not match the folder, which reads as '{expected}'.",
                paths.ToCaller(file),
                line));
        }

        return findings;
    }

    private static string Expected(Project project, Document document)
    {
        var prefix = string.IsNullOrWhiteSpace(project.DefaultNamespace) ? project.Name : project.DefaultNamespace;
        List<string> folders = [.. document.Folders.Where(folder => !string.IsNullOrWhiteSpace(folder))];

        return folders.Count == 0 ? prefix : $"{prefix}.{string.Join('.', folders)}";
    }

    private static IReadOnlyList<string> ProjectCycles(Solution solution)
    {
        List<string> cycles = [];

        foreach (var project in solution.Projects)
        {
            foreach (var reference in project.ProjectReferences)
            {
                var target = solution.GetProject(reference.ProjectId);

                if (target is not null && target.ProjectReferences.Any(back => back.ProjectId == project.Id))
                    cycles.Add($"{project.Name} -> {target.Name} -> {project.Name}");
            }
        }

        return [.. cycles.Distinct(StringComparer.Ordinal)];
    }

    private IReadOnlyList<SymbolMatch> Hotspots(SolutionUsage usage, int cap)
    {
        var used = usage.Entries.Where(entry => entry.Symbol.Kind == SymbolKind.NamedType && entry.References > 0);
        var ranked = used
            .OrderByDescending(entry => entry.Files.Count)
            .ThenByDescending(entry => entry.References);
        var top = ranked.Take(Math.Min(cap, 20));

        return [.. top.Select(entry => SymbolFacts.Match(entry.Symbol, paths))];
    }

    private IEnumerable<CodeIssue> Top(IEnumerable<Diagnostic> diagnostics, int cap) =>
        diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Where(diagnostic => SourcePaths.IsActionable(SourcePaths.Of(diagnostic)))
            .Take(cap)
            .Select(diagnostic => new CodeIssue(
                diagnostic.Id,
                IssueSeverity.Error,
                diagnostic.GetMessage(),
                SourcePaths.Of(diagnostic) is { } file ? paths.ToCaller(file) : null,
                diagnostic.Location.GetMappedLineSpan().StartLinePosition.Line + 1,
                diagnostic.Location.GetMappedLineSpan().StartLinePosition.Character + 1,
                1));

    private static int Count(Compilation compilation) =>
        compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type)
            .OfType<INamedTypeSymbol>()
            .Count(type => type.Locations.Any(location => location.IsInSource));

    private static bool IsCSharp(Project project) => project.Language == LanguageNames.CSharp;

    private static string? TargetOf(Project project)
    {
        var open = project.Name.IndexOf('(');

        return open < 0 ? null : project.Name[(open + 1)..].TrimEnd(')');
    }
}
