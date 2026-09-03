using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>The analyzers this server carries, for projects that reference none of their own.</summary>
internal static class ShippedAnalyzers
{
    private static readonly Lazy<ImmutableArray<DiagnosticAnalyzer>> Loaded = new(Load);

    public static ImmutableArray<DiagnosticAnalyzer> All => Loaded.Value;

    private static ImmutableArray<DiagnosticAnalyzer> Load()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "analyzers");

        if (!Directory.Exists(folder))
            return [];

        List<DiagnosticAnalyzer> analyzers = [];

        foreach (var file in Directory.EnumerateFiles(folder, "*.dll"))
        {
            var reference = new AnalyzerFileReference(file, Loader.Instance);

            analyzers.AddRange(reference.GetAnalyzers(LanguageNames.CSharp));
        }

        return [.. analyzers];
    }

    private sealed class Loader : IAnalyzerAssemblyLoader
    {
        public static readonly Loader Instance = new();

        public void AddDependencyLocation(string fullPath)
        {
        }

        public Assembly LoadFromPath(string fullPath) =>
            AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
    }
}
