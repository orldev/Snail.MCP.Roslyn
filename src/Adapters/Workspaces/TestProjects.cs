using Microsoft.CodeAnalysis;

namespace Snail.MCP.Roslyn.Adapters.Workspaces;

/// <summary>Tells a test project from a production one, by what it references rather than by what it is called.</summary>
internal static class TestProjects
{
    private static readonly string[] Frameworks = ["xunit", "nunit", "mstest", "Microsoft.VisualStudio.TestPlatform"];

    public static bool Is(Project project) =>
        project.MetadataReferences.Any(reference =>
            Frameworks.Any(framework =>
                reference.Display?.Contains(framework, StringComparison.OrdinalIgnoreCase) == true));

    public static HashSet<string> NamesIn(Solution solution) =>
        [.. solution.Projects.Where(Is).Select(project => project.Name)];
}
