namespace Snail.MCP.Roslyn.Tests;

[Collection(nameof(AnalysisCollection))]
public sealed class DiagnosticScopeTests(AnalysisFixture fixture)
{
    /// <summary>A file whose error the compiler reports against a path no project holds, which is the shape
    /// generated code has: the diagnostic belongs to this compilation but points somewhere else entirely.</summary>
    private const string MappedAway =
        """
        namespace Snail.MCP.Roslyn;

        internal sealed class MappedAwayFromItsFile
        {
        #line 1 "PHANTOM"
            public int Broken() => undefinedName;
        #line default
        }
        """;

    [Fact]
    public async Task ReadAsync_ScopeOfOneProject_ReportsEverythingTheWholeSolutionPassReportsThere()
    {
        var folder = Path.Combine(RepositoryPaths.Root, "tests");

        var scoped = await Issues(folder);
        var whole = await Issues(null);

        Assert.Equal(
            whole.Where(issue => issue.StartsWith(folder, StringComparison.Ordinal)).Order(),
            scoped.Order());
    }

    /// <summary>Only the projects holding a file under the scope are compiled, and no project holds this path.
    /// Answering "no issues" from that would be a confident lie about code that does have one.</summary>
    [Fact]
    public async Task ReadAsync_ScopeOnlyALineDirectivePointsAt_IsNotAnsweredEmpty()
    {
        var phantom = Path.Combine(RepositoryPaths.Root, "MappedAway.Phantom.cs");
        var file = Leftovers.MappedAway;

        try
        {
            await File.WriteAllTextAsync(
                file, MappedAway.Replace("PHANTOM", phantom, StringComparison.Ordinal), CancellationToken.None);

            var page = await fixture.Diagnostics.ReadAsync(
                fixture.SolutionId, phantom, IssueSeverity.Error, 20, false, CancellationToken.None);

            var error = Assert.Single(page.Items);

            Assert.Equal("CS0103", error.Id);
            Assert.Equal(phantom, error.File);
        }
        finally
        {
            File.Delete(file);
        }
    }

    private async Task<IEnumerable<string>> Issues(string? scope)
    {
        var page = await fixture.Diagnostics.ReadAsync(
            fixture.SolutionId, scope, IssueSeverity.Info, 200, true, CancellationToken.None);

        Assert.False(page.IsTruncated, "A truncated page cannot be compared with another taken at a different scope.");

        return page.Items.Select(issue => $"{issue.File}:{issue.Line} {issue.Id}");
    }
}
