using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>The analyzers a project's own build runs, as far as this server's policy lets them run in its process.</summary>
internal static class ProjectAnalyzers
{
    public const string BlockedNote =
        "Some analyzer assemblies were not run: they live outside the package cache, the .NET installation and "
        + "the solution, "
        + "and an analyzer is arbitrary code executed in this process. Set SNAIL_MCP_ROSLYN_ANALYZER_POLICY to 'all' to run them anyway.";

    /// <summary>The analyzers the project references, less those the policy keeps out of this process, which <paramref name="blocked"/> owns up to.</summary>
    public static ImmutableArray<DiagnosticAnalyzer> Own(Project project, string policy, out bool blocked)
    {
        var root = project.FilePath is { } file ? Path.GetDirectoryName(file) : null;
        List<DiagnosticAnalyzer> own = [];

        blocked = false;

        foreach (var reference in project.AnalyzerReferences)
        {
            if (!AnalyzerGuard.Allows(policy, reference.FullPath, root))
            {
                blocked = true;

                continue;
            }

            own.AddRange(reference.GetAnalyzers(LanguageNames.CSharp));
        }

        return [.. own];
    }

    /// <summary>Whether the project's analyzers can fail its build on code that compiles: it has some, and its warnings are errors.</summary>
    public static bool CanFailTheBuild(Project project) =>
        project.AnalyzerReferences.Count > 0
        && project.CompilationOptions?.GeneralDiagnosticOption == ReportDiagnostic.Error;
}
