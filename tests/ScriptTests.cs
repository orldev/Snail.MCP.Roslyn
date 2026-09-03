namespace Snail.MCP.Roslyn.Tests;

/// <summary>A script answers the question no fixed tool anticipates, and cannot change the code.</summary>
[Collection(nameof(AnalysisCollection))]
public sealed class ScriptTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task A_script_sees_the_loaded_solution()
    {
        var result = await fixture.Scripts.RunAsync(
            fixture.SolutionId,
            "Solution.Projects.Count()",
            CancellationToken.None);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.Equal("2", result.Value);
    }

    [Fact]
    public async Task A_script_can_aggregate_over_the_whole_solution_in_one_pass()
    {
        var result = await fixture.Scripts.RunAsync(
            fixture.SolutionId,
            "Types(\"Roslyn\").Count(t => t.DeclaredAccessibility == Accessibility.Internal)",
            CancellationToken.None);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.True(int.Parse(result.Value!) > 5, $"expected the internal adapters, saw {result.Value}");
    }

    [Fact]
    public async Task A_script_that_does_not_compile_returns_the_diagnostics_and_the_available_globals()
    {
        var result = await fixture.Scripts.RunAsync(fixture.SolutionId, "NoSuchGlobal.Whatever()", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Notes, note => note.Contains("Globals available", StringComparison.Ordinal));
    }
}
