namespace Snail.MCP.Roslyn.Tests;

[Collection(nameof(AnalysisCollection))]
public sealed class OverviewTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task OverviewAsync_LoadedRepository_NamesEveryProjectAndTheirEdges()
    {
        var overview = await fixture.Surveyor.OverviewAsync(fixture.SolutionId, 20, CancellationToken.None);

        Assert.Equal(3, overview.Projects);
        Assert.True(overview.Documents > 40, $"expected the whole repository, saw {overview.Documents} documents");
        Assert.Contains(overview.Graph, project => project.DependsOn.Contains("Snail.MCP.Roslyn"));
    }

    [Fact]
    public async Task OverviewAsync_LoadedRepository_RanksHotspotsWithIds()
    {
        var overview = await fixture.Surveyor.OverviewAsync(fixture.SolutionId, 20, CancellationToken.None);

        Assert.NotEmpty(overview.Hotspots);
        Assert.All(overview.Hotspots, hotspot => Assert.False(string.IsNullOrWhiteSpace(hotspot.Id)));
    }

    [Fact]
    public async Task CheckAsync_ThisRepository_ReportsNoFolderNamespaceMismatch()
    {
        var report = await fixture.Surveyor.CheckAsync(fixture.SolutionId, 100, CancellationToken.None);

        var mismatches = report.Findings.Where(finding => finding.Rule == "folder-namespace").ToList();

        Assert.True(mismatches.Count == 0, string.Join("; ", mismatches.Select(finding => finding.Message)));
    }

    /// <summary>The rule above passes on a clean repository even if the check stopped looking, so the detection
    /// itself needs a file that breaks it.</summary>
    [Fact]
    public async Task CheckAsync_NamespaceNotMatchingItsFolder_ReportsItAtItsLine()
    {
        var file = Path.Combine(RepositoryPaths.Source, "Domain", "MisplacedByTheTest.cs");

        await File.WriteAllTextAsync(
            file,
            """
            namespace Snail.MCP.Roslyn.NotTheFolder;

            internal sealed record MisplacedByTheTest(int Value);
            """,
            CancellationToken.None);

        try
        {
            var report = await fixture.Surveyor.CheckAsync(fixture.SolutionId, 100, CancellationToken.None);

            var finding = Assert.Single(
                report.Findings,
                candidate => candidate.Rule == "folder-namespace"
                             && candidate.File?.EndsWith("MisplacedByTheTest.cs", StringComparison.Ordinal) == true);

            Assert.Equal(1, finding.Line);
            Assert.Contains("Snail.MCP.Roslyn.NotTheFolder", finding.Message);
            Assert.Contains("Snail.MCP.Roslyn.Domain", finding.Message);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task HealthAsync_ReadyWorkspace_ReportsProjectCountAndMemory()
    {
        var health = await fixture.Surveyor.HealthAsync(fixture.SolutionId, CancellationToken.None);

        Assert.Equal(WorkspaceReadiness.Ready, health.Readiness);
        Assert.Equal(3, health.Projects);
        Assert.True(health.WorkingSetMegabytes > 0);
    }
}
