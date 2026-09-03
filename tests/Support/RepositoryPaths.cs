namespace Snail.MCP.Roslyn.Tests.Support;

/// <summary>Locates the repository being analysed, which is this repository itself.</summary>
public static class RepositoryPaths
{
    public static string Root { get; } = Find();

    public static string Source => Path.Combine(Root, "src");

    private static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (directory.GetFiles("*.sln").Length > 0)
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("No solution file found above the test assembly.");
    }
}
