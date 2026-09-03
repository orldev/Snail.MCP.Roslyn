namespace Snail.MCP.Roslyn.Tests;

/// <summary>Impact is measured before a change, grouped so the blast radius is visible.</summary>
[Collection(nameof(AnalysisCollection))]
public sealed class ImpactTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task A_port_used_across_the_repository_reports_more_than_one_project()
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
    public async Task An_interface_member_reports_the_implementations_a_shape_change_would_break()
    {
        var report = await fixture.Impact.AnalyzeAsync(
            fixture.SolutionId,
            await fixture.MemberAsync(nameof(IPathMap.ToProcess), nameof(IPathMap)),
            50,
            CancellationToken.None);

        Assert.NotEmpty(report.Implementations);
    }

    [Fact]
    public async Task An_unknown_name_answers_with_a_note_rather_than_an_empty_report()
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
