namespace Snail.MCP.Roslyn.Tests;

/// <summary>Answers reflect the files as they are now, including files that appear after loading.</summary>
[Collection(nameof(AnalysisCollection))]
public sealed class FreshnessTests(AnalysisFixture fixture)
{
    private const string Broken =
        """
        namespace Snail.MCP.Roslyn;

        internal sealed class AppearedAfterLoading
        {
            public int Broken() => undefinedName;
        }
        """;

    [Fact]
    public async Task A_file_added_after_loading_is_analysed_rather_than_ignored()
    {
        var file = Path.Combine(RepositoryPaths.Source, "AppearedAfterLoading.cs");

        var before = await Errors();

        try
        {
            await File.WriteAllTextAsync(file, Broken, CancellationToken.None);

            var during = await Errors();

            Assert.True(during > before, "A file added after loading must be seen; a stale answer is a false clean bill.");
        }
        finally
        {
            File.Delete(file);
        }

        Assert.Equal(before, await Errors());
    }

    [Fact]
    public async Task Diagnostics_never_point_at_code_the_caller_cannot_edit()
    {
        var page = await fixture.Diagnostics.ReadAsync(
            fixture.SolutionId, null, IssueSeverity.Info, 200, false, CancellationToken.None);

        Assert.All(page.Items, item => Assert.DoesNotContain($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", item.File ?? string.Empty));
    }

    [Fact]
    public async Task Truncation_is_stated_rather_than_silent()
    {
        var page = await fixture.Diagnostics.ReadAsync(
            fixture.SolutionId, null, IssueSeverity.Info, 1, true, CancellationToken.None);

        Assert.Equal(page.Total > page.Items.Count, page.Truncated);
    }

    private async Task<int> Errors()
    {
        var page = await fixture.Diagnostics.ReadAsync(
            fixture.SolutionId, null, IssueSeverity.Error, 200, false, CancellationToken.None);

        return page.Total;
    }
}
