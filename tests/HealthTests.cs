namespace Snail.MCP.Roslyn.Tests;

[Collection(nameof(AnalysisCollection))]
public sealed class HealthTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task HealthAsync_AfterAScriptRan_CountsItAndSaysWhatItCostThisProcess()
    {
        var before = await fixture.Surveyor.HealthAsync(fixture.SolutionId, CancellationToken.None);

        var script = await fixture.Scripts.RunAsync(
            fixture.SolutionId, "Solution.Projects.Count()", CancellationToken.None);

        Assert.True(script.Succeeded, string.Join("; ", script.Errors));

        var after = await fixture.Surveyor.HealthAsync(fixture.SolutionId, CancellationToken.None);

        Assert.Equal(before.ScriptsRun + 1, after.ScriptsRun);
        Assert.True(after.AssembliesLoaded > 0);
        Assert.Contains(
            after.Notes,
            note => note.Contains("no collection returns", StringComparison.Ordinal));
    }
}

/// <summary>The advice is about the process, so it needs a server of its own with the ceiling set below what
/// any process holds.</summary>
public sealed class MemoryAdviceTests : IDisposable
{
    private const string Project =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """;

    private readonly string _folder = TestFolder.Create();

    [Fact]
    public async Task HealthAsync_WorkingSetPastTheCeiling_SaysARestartIsWhatRecoversIt()
    {
        await File.WriteAllTextAsync(Path.Combine(_folder, "Tiny.csproj"), Project);
        await File.WriteAllTextAsync(Path.Combine(_folder, "Widget.cs"), "namespace Tiny;\n\npublic sealed class Widget;\n");

        using var fixture = new AnalysisFixture(
            [new KeyValuePair<string, string?>("MemoryAdviceMegabytes", "1")]);

        var loaded = await fixture.Host.LoadAsync(_folder, CancellationToken.None);
        var health = await fixture.Surveyor.HealthAsync(loaded.Id, CancellationToken.None);

        Assert.True(health.WorkingSetMegabytes > 1);
        Assert.Contains(health.Notes, note => note.Contains("restart the server", StringComparison.Ordinal));
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
