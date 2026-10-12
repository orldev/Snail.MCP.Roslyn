namespace Snail.MCP.Roslyn.Tests;

/// <summary>
/// This is why the server moved into a container: it outlives a step. Opening a solution is the expensive part,
/// seconds on a small repository and minutes on a large one, and a process that lived one step paid it every time.
/// </summary>
public sealed class WarmCacheTests : IDisposable
{
    private const string Project =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """;

    private readonly string _folder = Directory.CreateTempSubdirectory("roslyn-warm").FullName;

    [Fact]
    public async Task LoadAsync_SolutionAlreadyLoaded_IsReusedRatherThanOpenedAgain()
    {
        await File.WriteAllTextAsync(Path.Combine(_folder, "Warm.csproj"), Project);
        await File.WriteAllTextAsync(Path.Combine(_folder, "Widget.cs"), "namespace Warm; public sealed class Widget;");

        using var fixture = new AnalysisFixture();

        var first = await fixture.Host.LoadAsync(_folder, CancellationToken.None);
        var second = await fixture.Host.LoadAsync(_folder, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.DoesNotContain(first.Notes, note => note.Contains("was reused", StringComparison.Ordinal));
        Assert.Contains(second.Notes, note => note.Contains("was reused", StringComparison.Ordinal));
        Assert.True(second.ElapsedMilliseconds < first.ElapsedMilliseconds, $"{second.ElapsedMilliseconds} ms against {first.ElapsedMilliseconds} ms");
    }

    /// <summary>An edit made by one step is seen by the next: the cache is reused, but does not go stale.</summary>
    [Fact]
    public async Task A_file_changed_between_loads_is_read_again()
    {
        var source = Path.Combine(_folder, "Widget.cs");

        await File.WriteAllTextAsync(Path.Combine(_folder, "Warm.csproj"), Project);
        await File.WriteAllTextAsync(source, "namespace Warm; public sealed class Widget;");

        using var fixture = new AnalysisFixture();

        await fixture.Host.LoadAsync(_folder, CancellationToken.None);
        await File.WriteAllTextAsync(source, "namespace Warm; public sealed class Gadget;");

        var reloaded = await fixture.Host.LoadAsync(_folder, CancellationToken.None);
        var found = await fixture.Symbols.SearchAsync(reloaded.Id, "Gadget", SymbolKindFilter.Any, 10, CancellationToken.None);

        Assert.Contains(found, symbol => symbol.Name == "Gadget");
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
