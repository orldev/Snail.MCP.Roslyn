namespace Snail.MCP.Roslyn.Tests;

/// <summary>Generated output is shown as unreachable for editing, never as ordinary source.</summary>
[Collection(nameof(AnalysisCollection))]
public sealed class GeneratedCodeTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task The_answer_says_that_editing_generated_files_changes_nothing()
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
    public async Task An_unknown_project_answers_with_a_note_rather_than_silence()
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
