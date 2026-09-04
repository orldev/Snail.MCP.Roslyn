namespace Snail.MCP.Roslyn.Tests;

[Collection(nameof(AnalysisCollection))]
public sealed class RegistrationTests(AnalysisFixture fixture)
{
    [Fact]
    public async Task ReadAsync_RegisteredPort_ReportsLifetimeImplementationAndFile()
    {
        var report = await fixture.Container.ReadAsync(fixture.SolutionId, null, 200, CancellationToken.None);

        var port = Assert.Single(report.Items, item => item.Service.EndsWith(nameof(IPathMap), StringComparison.Ordinal));

        Assert.Equal("Singleton", port.Lifetime);
        Assert.EndsWith("RootPathMap", port.Implementation);
        Assert.EndsWith("ServiceCollectionExtensions.cs", port.File, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_AnyResult_WarnsThatByNameRegistrationsSurviveRename()
    {
        var report = await fixture.Container.ReadAsync(fixture.SolutionId, null, 200, CancellationToken.None);

        Assert.Contains(report.Notes, note => note.Contains("rename_symbol", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReadAsync_ServiceFilter_KeepsOnlyMatchingRegistrations()
    {
        var report = await fixture.Container.ReadAsync(fixture.SolutionId, "IScriptRunner", 200, CancellationToken.None);

        Assert.All(report.Items, item => Assert.Contains("IScriptRunner", item.Service, StringComparison.Ordinal));
    }
}
