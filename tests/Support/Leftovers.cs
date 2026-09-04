namespace Snail.MCP.Roslyn.Tests.Support;

/// <summary>The files this suite writes into the source tree while a test needs the compiler to see them.</summary>
/// <remarks>Each test deletes its own file in a finally, which a killed run never reaches. What is left behind
/// does not compile, so the next build fails on code nobody wrote — and it fails before any test runs, which is
/// why the sweep that removes them is a target in the test project rather than anything written in C# here.
/// That target matches the Prefix, so a new artifact is only swept if it is named through this class.</remarks>
public static class Leftovers
{
    /// <summary>Shared by the file names below and by the SweepLeftovers target in the test project.</summary>
    private const string Prefix = "SuiteArtifact.";

    /// <summary>A file with a compiler error, added after the solution was loaded.</summary>
    public static string Appeared { get; } = InSource("Appeared");

    /// <summary>A file read back through read_file while it does not compile.</summary>
    public static string Unreadable { get; } = InSource("Unreadable");

    /// <summary>A file whose error the compiler maps, by #line, to a path no project holds.</summary>
    public static string MappedAway { get; } = InSource("MappedAway");

    private static string InSource(string name) =>
        Path.Combine(RepositoryPaths.Source, $"{Prefix}{name}.cs");
}
