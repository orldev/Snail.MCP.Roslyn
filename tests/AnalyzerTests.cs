namespace Snail.MCP.Roslyn.Tests;

[Collection(nameof(AnalysisCollection))]
public sealed class AnalyzerTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task RunAsync_LoadedSolution_NamesAnalyzersThatRanAndStatesCost()
    {
        var page = await fixture.Analyzers.RunAsync(
            fixture.SolutionId,
            RepositoryPaths.Source,
            null,
            IssueSeverity.Info,
            50,
            CancellationToken.None);

        Assert.NotEmpty(page.Analyzers);
        Assert.Contains(page.Notes, note => note.Contains("seconds, not milliseconds", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_AnyFinding_CarriesCategoryAndFile()
    {
        var page = await fixture.Analyzers.RunAsync(
            fixture.SolutionId,
            RepositoryPaths.Source,
            null,
            IssueSeverity.Info,
            50,
            CancellationToken.None);

        Assert.All(page.Items, issue => Assert.False(string.IsNullOrWhiteSpace(issue.Category)));
        Assert.All(page.Items, issue => Assert.NotNull(issue.File));
    }

    [Fact]
    public async Task FixesAsync_LineWithoutDiagnostic_ReturnsNoFixesWithNote()
    {
        var page = await fixture.Analyzers.FixesAsync(
            fixture.SolutionId,
            Path.Combine(RepositoryPaths.Source, "Domain", "LineEdit.cs"),
            1,
            null,
            CancellationToken.None);

        Assert.Empty(page.Fixes);
        Assert.NotEmpty(page.Notes);
    }
}
