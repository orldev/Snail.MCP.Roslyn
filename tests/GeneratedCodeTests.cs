namespace Snail.MCP.Roslyn.Tests;

[Collection(nameof(AnalysisCollection))]
public sealed class GeneratedCodeTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task ReadAsync_ProjectWithGeneratedOutput_NamesGeneratorAndWarnsAgainstEditing()
    {
        var page = await fixture.Generated.ReadAsync(
            fixture.SolutionId,
            "Snail.MCP.Roslyn",
            null,
            false,
            20,
            CancellationToken.None);

        Assert.NotEmpty(page.Notes);
        Assert.All(page.Files, file => Assert.False(string.IsNullOrWhiteSpace(file.Generator)));
    }

    [Fact]
    public async Task ReadAsync_UnknownProject_ReturnsNoFilesWithExplanatoryNote()
    {
        var page = await fixture.Generated.ReadAsync(
            fixture.SolutionId,
            "NoSuchProject",
            null,
            false,
            20,
            CancellationToken.None);

        Assert.Empty(page.Files);
        Assert.Contains(page.Notes, note => note.Contains("load_solution", StringComparison.Ordinal));
    }
}
