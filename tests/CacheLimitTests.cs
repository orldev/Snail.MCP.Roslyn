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

    private readonly string first = Directory.CreateTempSubdirectory("roslyn-cache-first").FullName;

    private readonly string second = Directory.CreateTempSubdirectory("roslyn-cache-second").FullName;

    [Fact]
    public async Task FreshAsync_SolutionDroppedForTheCeiling_SaysSoInsteadOfClaimingItWasNeverLoaded()
    {
        await WriteAsync(first, "First");
        await WriteAsync(second, "Second");

        using var fixture = new AnalysisFixture([new KeyValuePair<string, string?>("Workspace:MaxSolutions", "1")]);

        var dropped = (await fixture.Host.LoadAsync(first, CancellationToken.None)).Id;
        var kept = (await fixture.Host.LoadAsync(second, CancellationToken.None)).Id;

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Diagnostics.ReadAsync(dropped, null, IssueSeverity.Error, 10, false, CancellationToken.None));

        Assert.Contains("dropped", failure.Message, StringComparison.Ordinal);

        var health = await fixture.Surveyor.HealthAsync(kept, CancellationToken.None);

        Assert.Equal(1, health.LoadedSolutions);
    }

    public void Dispose()
    {
        Directory.Delete(first, recursive: true);
        Directory.Delete(second, recursive: true);
    }

    private static async Task WriteAsync(string folder, string name)
    {
        await File.WriteAllTextAsync(Path.Combine(folder, $"{name}.csproj"), Project);

        await File.WriteAllTextAsync(
            Path.Combine(folder, "Widget.cs"),
            $"namespace {name};{Environment.NewLine}{Environment.NewLine}public sealed class Widget;{Environment.NewLine}");
    }
}
