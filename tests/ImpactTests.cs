namespace Snail.MCP.Roslyn.Tests;

[Collection(nameof(AnalysisCollection))]
public sealed class ImpactTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task AnalyzeAsync_SymbolUsedInSeveralProjects_ReportsCrossProjectImpact()
    {
        var report = await fixture.Impact.AnalyzeAsync(
            fixture.SolutionId,
            new SymbolQuery(null, nameof(ISolutionHost), null, 0, 0),
            50,
            CancellationToken.None);

        Assert.True(report.References > 0);
        Assert.True(report.CrossesProjects, "the port is referenced from the tests as well as from the server");
        Assert.Contains(report.Projects, site => site.Project.Contains("Tests", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnalyzeAsync_InterfaceMember_ReportsImplementationsAShapeChangeBreaks()
    {
        var report = await fixture.Impact.AnalyzeAsync(
            fixture.SolutionId,
            await fixture.MemberAsync(nameof(IPathMap.ToProcess), nameof(IPathMap)),
            50,
            CancellationToken.None);

        Assert.NotEmpty(report.Implementations);
    }

    [Fact]
    public async Task AnalyzeAsync_UnknownName_ReturnsZeroReferencesWithNote()
    {
        var report = await fixture.Impact.AnalyzeAsync(
            fixture.SolutionId,
            new SymbolQuery(null, "NoSuchSymbolAnywhere", null, 0, 0),
            50,
            CancellationToken.None);

        Assert.Equal(0, report.References);
        Assert.NotEmpty(report.Notes);
    }
}
