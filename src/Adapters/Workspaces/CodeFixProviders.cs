using System.Reflection;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>The code fix providers this process can construct, matched to the diagnostics they claim to repair.</summary>
internal static class CodeFixProviders
{
    private const string Features = "Microsoft.CodeAnalysis.CSharp.Features";

    private static readonly Lazy<IReadOnlyList<CodeFixProvider>> Loaded = new(Load);

    /// <summary>How many providers this process could construct, so an empty answer can say why it is empty.</summary>
    public static int Count => Loaded.Value.Count;

    public static IEnumerable<CodeFixProvider> For(string diagnosticId) =>
        Loaded.Value.Where(provider => provider.FixableDiagnosticIds.Contains(diagnosticId));

    private static IReadOnlyList<CodeFixProvider> Load() =>
        [.. Candidates().Select(Construct).OfType<CodeFixProvider>()];

    private static IEnumerable<Type> Candidates()
    {
        if (Resolve() is not { } assembly)
            return [];

        try
        {
            return assembly.GetTypes().Where(IsProvider);
        }
        catch (ReflectionTypeLoadException error)
        {
            return error.Types.OfType<Type>().Where(IsProvider);
        }
    }

    private static bool IsProvider(Type type) => type.IsSubclassOf(typeof(CodeFixProvider)) && !type.IsAbstract;

    private static Assembly? Resolve()
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(candidate => candidate.GetName().Name == Features);

        if (loaded is not null)
            return loaded;

        try
        {
            return Assembly.Load(Features);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static CodeFixProvider? Construct(Type type)
    {
        if (type.GetConstructor(Type.EmptyTypes) is null)
            return null;

        try
        {
            return Activator.CreateInstance(type) as CodeFixProvider;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
