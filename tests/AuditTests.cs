namespace Snail.MCP.Roslyn.Tests;

[Collection(nameof(AnalysisCollection))]
public sealed class AuditTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task UnusedAsync_SymbolReferencedElsewhere_IsNotReported()
    {
        var page = await fixture.Auditor.UnusedAsync(fixture.SolutionId, RepositoryPaths.Source, 300, CancellationToken.None);

        Assert.DoesNotContain(page.Items, finding => finding.Symbol.Name == "RootPathMap");
        Assert.DoesNotContain(page.Items, finding => finding.Symbol.Name == nameof(SolutionOverview));
    }

    [Fact]
    public async Task UnusedAsync_AnyFinding_CarriesReasonAndReflectionCaveat()
    {
        var page = await fixture.Auditor.UnusedAsync(fixture.SolutionId, RepositoryPaths.Source, 50, CancellationToken.None);

        Assert.All(page.Items, finding => Assert.False(string.IsNullOrWhiteSpace(finding.Reason)));
        Assert.Contains(page.Notes, note => note.Contains("Reflection", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UncoveredAsync_SolutionWithTestProject_ExcludesSymbolsTheTestsReach()
    {
        var page = await fixture.Auditor.UncoveredAsync(fixture.SolutionId, RepositoryPaths.Source, 300, CancellationToken.None);

        Assert.DoesNotContain(page.Notes, note => note.Contains("No test project", StringComparison.Ordinal));
        Assert.DoesNotContain(page.Items, finding => finding.Symbol.Name == nameof(WorkspaceReport));
    }

    [Fact]
    public async Task TestsForAsync_SymbolUsedInTests_ReturnsLocatedTestMethods()
    {
        var tests = await fixture.Auditor.TestsForAsync(
            fixture.SolutionId,
            new SymbolQuery(null, nameof(ISolutionHost), null, 0, 0),
            50,
            CancellationToken.None);

        Assert.NotEmpty(tests);
        Assert.All(tests, match => Assert.NotNull(match.File));
    }
}
