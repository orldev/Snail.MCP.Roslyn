namespace Snail.MCP.Roslyn.Tests;

/// <summary>The solution-wide answers describe this repository as it actually is.</summary>
[Collection(nameof(AnalysisCollection))]
public sealed class OverviewTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task Overview_names_both_projects_and_the_edge_between_them()
    {
        var overview = await fixture.Surveyor.OverviewAsync(fixture.SolutionId, 20, CancellationToken.None);

        Assert.Equal(2, overview.Projects);
        Assert.True(overview.Documents > 40, $"expected the whole repository, saw {overview.Documents} documents");
        Assert.Contains(overview.Graph, project => project.DependsOn.Contains("Snail.MCP.Roslyn"));
    }

    [Fact]
    public async Task Overview_ranks_the_types_the_rest_of_the_code_names_most_often()
    {
        var overview = await fixture.Surveyor.OverviewAsync(fixture.SolutionId, 20, CancellationToken.None);

        Assert.NotEmpty(overview.Hotspots);
        Assert.All(overview.Hotspots, hotspot => Assert.False(string.IsNullOrWhiteSpace(hotspot.Id)));
    }

    [Fact]
    public async Task Folders_and_namespaces_agree_across_this_repository()
    {
        var report = await fixture.Surveyor.CheckAsync(fixture.SolutionId, 100, CancellationToken.None);

        var mismatches = report.Findings.Where(finding => finding.Rule == "folder-namespace").ToList();

        Assert.True(mismatches.Count == 0, string.Join("; ", mismatches.Select(finding => finding.Message)));
    }

    [Fact]
    public async Task Health_reports_a_ready_workspace_and_the_cost_of_a_diagnostic_pass()
    {
        var health = await fixture.Surveyor.HealthAsync(fixture.SolutionId, CancellationToken.None);

        Assert.Equal(WorkspaceReadiness.Ready, health.Readiness);
        Assert.Equal(2, health.Projects);
        Assert.True(health.WorkingSetMegabytes > 0);
    }
}
