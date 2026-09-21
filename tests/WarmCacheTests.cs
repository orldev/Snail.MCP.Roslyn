namespace Snail.MCP.Roslyn.Tests;

/// <summary>
/// Ради этого сервер и переехал в контейнер: он живёт дольше шага. Открытие решения — дорогая часть,
/// секунды на маленьком репозитории и минуты на большом, и процесс, живший один шаг, платил её каждый раз.
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

    private readonly string folder = Directory.CreateTempSubdirectory("roslyn-warm").FullName;

    [Fact]
    public async Task LoadAsync_SolutionAlreadyLoaded_IsReusedRatherThanOpenedAgain()
    {
        await File.WriteAllTextAsync(Path.Combine(folder, "Warm.csproj"), Project);
        await File.WriteAllTextAsync(Path.Combine(folder, "Widget.cs"), "namespace Warm; public sealed class Widget;");

        using var fixture = new AnalysisFixture();

        var first = await fixture.Host.LoadAsync(folder, CancellationToken.None);
        var second = await fixture.Host.LoadAsync(folder, CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.DoesNotContain(first.Notes, note => note.Contains("was reused", StringComparison.Ordinal));
        Assert.Contains(second.Notes, note => note.Contains("was reused", StringComparison.Ordinal));
        Assert.True(second.ElapsedMilliseconds < first.ElapsedMilliseconds, $"{second.ElapsedMilliseconds} ms против {first.ElapsedMilliseconds} ms");
    }

    /// <summary>Правка, сделанная шагом, видна следующему: кэш переиспользуется, но не устаревает.</summary>
    [Fact]
    public async Task A_file_changed_between_loads_is_read_again()
    {
        var source = Path.Combine(folder, "Widget.cs");

        await File.WriteAllTextAsync(Path.Combine(folder, "Warm.csproj"), Project);
        await File.WriteAllTextAsync(source, "namespace Warm; public sealed class Widget;");

        using var fixture = new AnalysisFixture();

        await fixture.Host.LoadAsync(folder, CancellationToken.None);
        await File.WriteAllTextAsync(source, "namespace Warm; public sealed class Gadget;");

        var reloaded = await fixture.Host.LoadAsync(folder, CancellationToken.None);
        var found = await fixture.Symbols.SearchAsync(reloaded.Id, "Gadget", SymbolKindFilter.Any, 10, CancellationToken.None);

        Assert.Contains(found, symbol => symbol.Name == "Gadget");
    }

    public void Dispose() => Directory.Delete(folder, recursive: true);
}
