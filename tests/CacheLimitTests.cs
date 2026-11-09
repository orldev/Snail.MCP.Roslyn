using Snail.MCP.Roslyn.Adapters.Workspaces;
using Snail.Sdk.Mcp.Hosting.Answers;

namespace Snail.MCP.Roslyn.Tests;

public sealed class CacheLimitTests : IDisposable
{
    private const string Project =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """;

    private readonly string _first = TestFolder.Create();

    private readonly string _second = TestFolder.Create();

    [Fact]
    public async Task FreshAsync_SolutionDroppedForTheCeiling_SaysSoInsteadOfClaimingItWasNeverLoaded()
    {
        await WriteAsync(_first, "First");
        await WriteAsync(_second, "Second");

        using var fixture = new AnalysisFixture([new KeyValuePair<string, string?>("MaxSolutions", "1")]);

        var dropped = (await fixture.Host.LoadAsync(_first, CancellationToken.None)).Id;
        var kept = (await fixture.Host.LoadAsync(_second, CancellationToken.None)).Id;

        var failure = await Assert.ThrowsAsync<ToolRefusalException>(() =>
            fixture.Diagnostics.ReadAsync(dropped, null, IssueSeverity.Error, 10, false, CancellationToken.None));

        Assert.Contains("dropped", failure.Error, StringComparison.Ordinal);
        Assert.Equal(RefusalTypes.NotLoaded, failure.Type);
        Assert.Equal("Call load_solution again.", failure.Hint);

        var health = await fixture.Surveyor.HealthAsync(kept, CancellationToken.None);

        Assert.Equal(1, health.LoadedSolutions);
    }

    public void Dispose()
    {
        Directory.Delete(_first, recursive: true);
        Directory.Delete(_second, recursive: true);
    }

    private static async Task WriteAsync(string folder, string name)
    {
        await File.WriteAllTextAsync(Path.Combine(folder, $"{name}.csproj"), Project);

        await File.WriteAllTextAsync(
            Path.Combine(folder, "Widget.cs"),
            $"namespace {name};{Environment.NewLine}{Environment.NewLine}public sealed class Widget;{Environment.NewLine}");
    }
}
