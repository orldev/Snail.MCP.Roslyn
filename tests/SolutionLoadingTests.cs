using Snail.MCP.Roslyn.Adapters.Workspaces;
using Snail.Sdk.Mcp.Hosting.Answers;

namespace Snail.MCP.Roslyn.Tests;

[Collection(nameof(AnalysisCollection))]
public sealed class SolutionLoadingTests(AnalysisFixture fixture)
{
    [Fact]
    public void LoadAsync_RestoredRepository_ReportsReadyWithSemantics()
    {
        Assert.Equal(WorkspaceReadiness.Ready, fixture.Report.Readiness);
        Assert.NotEmpty(fixture.Report.Projects);
        Assert.All(fixture.Report.Projects, project => Assert.True(project.HasSemantics));
    }

    [Fact]
    public void LoadAsync_ProjectWithoutMetadataReferences_IsNeverReportedReady()
    {
        Assert.All(
            fixture.Report.Projects.Where(project => !project.HasSemantics),
            _ => Assert.NotEqual(WorkspaceReadiness.Ready, fixture.Report.Readiness));
    }

    [Fact]
    public async Task LoadAsync_PathWithoutProject_IsRefusedAsNotFound()
    {
        var refused = await Assert.ThrowsAsync<ToolRefusalException>(() =>
            fixture.Host.LoadAsync(Path.GetTempPath(), CancellationToken.None));

        Assert.Equal(RefusalTypes.NotFound, refused.Type);
        Assert.Contains("No solution or project found", refused.Error, StringComparison.Ordinal);
        Assert.NotNull(refused.Hint);
    }

    [Fact]
    public async Task Unload_LoadedSolution_ReleasesItAndRejectsLaterQuestions()
    {
        var folder = Directory.CreateTempSubdirectory("roslyn-unload").FullName;

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(folder, "Tiny.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            await File.WriteAllTextAsync(Path.Combine(folder, "Tiny.cs"), "namespace Tiny; public sealed class Thing;");

            var report = await fixture.Host.LoadAsync(folder, CancellationToken.None);
            var unloaded = await fixture.Host.UnloadAsync(report.Id, CancellationToken.None);
            var again = await fixture.Host.UnloadAsync(report.Id, CancellationToken.None);

            Assert.True(unloaded.WasUnloaded);
            Assert.False(again.WasUnloaded);
            Assert.Contains(unloaded.Notes, note => note.Contains("Load it again", StringComparison.Ordinal));

            var refused = await Assert.ThrowsAsync<ToolRefusalException>(() =>
                fixture.Diagnostics.ReadAsync(report.Id, null, IssueSeverity.Warning, 5, false, CancellationToken.None));

            Assert.Equal(RefusalTypes.NotLoaded, refused.Type);
            Assert.Equal("Call load_solution first.", refused.Hint);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
