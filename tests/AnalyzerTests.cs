namespace Snail.MCP.Roslyn.Tests;

/// <summary>Analyzers run over the solution and state what they cost and which ones ran.</summary>
[Collection(nameof(AnalysisCollection))]
public sealed class AnalyzerTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task Running_the_analyzers_names_the_analyzers_that_ran()
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
    public async Task Every_finding_carries_the_category_that_explains_why_it_matters()
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
    public async Task A_line_with_no_diagnostic_answers_with_a_note_rather_than_an_empty_list()
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
