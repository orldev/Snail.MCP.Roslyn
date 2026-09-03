namespace Snail.MCP.Roslyn.Tests;

/// <summary>Audits report checkable claims, and say what they cannot see.</summary>
[Collection(nameof(AnalysisCollection))]
public sealed class AuditTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task Nothing_that_the_tests_exercise_is_reported_as_unused()
    {
        var page = await fixture.Auditor.UnusedAsync(fixture.SolutionId, RepositoryPaths.Source, 300, CancellationToken.None);

        Assert.DoesNotContain(page.Items, finding => finding.Symbol.Name == "RootPathMap");
        Assert.DoesNotContain(page.Items, finding => finding.Symbol.Name == nameof(SolutionOverview));
    }

    [Fact]
    public async Task Every_unused_finding_states_the_claim_it_makes_and_the_limits_of_it()
    {
        var page = await fixture.Auditor.UnusedAsync(fixture.SolutionId, RepositoryPaths.Source, 50, CancellationToken.None);

        Assert.All(page.Items, finding => Assert.False(string.IsNullOrWhiteSpace(finding.Reason)));
        Assert.Contains(page.Notes, note => note.Contains("Reflection", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Uncovered_surface_is_measured_against_the_test_project_that_exists()
    {
        var page = await fixture.Auditor.UncoveredAsync(fixture.SolutionId, RepositoryPaths.Source, 300, CancellationToken.None);

        Assert.DoesNotContain(page.Notes, note => note.Contains("No test project", StringComparison.Ordinal));
        Assert.DoesNotContain(page.Items, finding => finding.Symbol.Name == nameof(WorkspaceReport));
    }

    [Fact]
    public async Task A_symbol_the_tests_reach_names_the_test_methods_that_reach_it()
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
