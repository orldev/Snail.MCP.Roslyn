namespace Snail.MCP.Roslyn.Tests;

[Collection(nameof(AnalysisCollection))]
public sealed class ConcurrencyTests(AnalysisFixture fixture)
{
    private const int Callers = 16;

    [Fact]
    public async Task Diagnostics_ManyConcurrentCallers_AgreeAndDoNotFault()
    {
        var calls = Enumerable.Range(0, Callers).Select(_ => fixture.Diagnostics.ReadAsync(
            fixture.SolutionId, null, IssueSeverity.Warning, 200, false, CancellationToken.None));

        var pages = await Task.WhenAll(calls);

        Assert.All(pages, page => Assert.Equal(pages[0].Total, page.Total));
    }

    [Fact]
    public async Task MixedReaders_RunningTogether_EachReturnItsOwnAnswer()
    {
        var file = Path.Combine(RepositoryPaths.Source, "Adapters", "Workspaces", "SymbolResolver.cs");

        var read = fixture.Reader.ReadAsync(fixture.SolutionId, file, 1, 2000, CancellationToken.None);
        var search = fixture.Symbols.SearchAsync(
            fixture.SolutionId, nameof(RepositoryPaths), SymbolKindFilter.Type, 10, CancellationToken.None);
        var overview = fixture.Surveyor.OverviewAsync(fixture.SolutionId, 20, CancellationToken.None);
        var health = fixture.Surveyor.HealthAsync(fixture.SolutionId, CancellationToken.None);

        await Task.WhenAll(read, search, overview, health);

        Assert.Contains("SymbolResolver", (await read).Text, StringComparison.Ordinal);
        Assert.Contains(await search, match => match.Name == nameof(RepositoryPaths));
        Assert.Equal(3, (await overview).Projects);
        Assert.Equal(WorkspaceReadiness.Ready, (await health).Readiness);
    }
}
