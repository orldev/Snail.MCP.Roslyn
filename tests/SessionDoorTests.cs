namespace Snail.MCP.Roslyn.Tests;

/// <summary>
/// Две двери, через которые путь попадает внутрь: загрузка решения по пути и всё остальное по его id.
/// Вторая тише и потому опаснее — id считается из пути детерминированно, поэтому в общем контейнере
/// чужой id не надо угадывать, его можно вычислить.
/// </summary>
public sealed class SessionDoorTests : IDisposable
{
    private const string Project =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """;

    private readonly string mine = Directory.CreateTempSubdirectory("roslyn-door-mine").FullName;

    private readonly string theirs = Directory.CreateTempSubdirectory("roslyn-door-theirs").FullName;

    [Fact]
    public async Task LoadSolution_APathOutsideTheSession_IsRefusedAndSaysWhere()
    {
        await WriteAsync(theirs, "Theirs");

        using var fixture = new AnalysisFixture([], new Address(mine));

        var report = await fixture.Host.LoadAsync(theirs, CancellationToken.None);

        var said = string.Join(" ", report.Notes);

        Assert.Contains("outside this session", said, StringComparison.Ordinal);
        Assert.Contains(mine, said, StringComparison.Ordinal);
    }

    /// <summary>
    /// Решение, загруженное другой сессией, остаётся в общем кэше процесса — и по id к нему не пускают.
    /// Без этой двери один воркер читал бы файлы и диагностику чужой задачи, зная только её путь.
    /// </summary>
    [Fact]
    public async Task ASolutionLoadedByAnotherSession_IsNotReachableByItsId()
    {
        await WriteAsync(theirs, "Theirs");

        var address = new Address(theirs);
        using var fixture = new AnalysisFixture([], address);

        var loaded = await fixture.Host.LoadAsync(theirs, CancellationToken.None);
        Assert.NotEmpty(loaded.Id);

        address.Root = mine;

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Diagnostics.ReadAsync(loaded.Id, null, IssueSeverity.Error, 10, false, CancellationToken.None));

        Assert.Contains("another session", refused.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Directory.Delete(mine, recursive: true);
        Directory.Delete(theirs, recursive: true);
    }

    private static async Task WriteAsync(string folder, string name)
    {
        await File.WriteAllTextAsync(Path.Combine(folder, $"{name}.csproj"), Project);
        await File.WriteAllTextAsync(Path.Combine(folder, "Type.cs"), $"namespace {name}; public sealed class Type;");
    }

    private sealed class Address(string root) : ISessionAddress
    {
        public string Root { get; set; } = root;

        public IReadOnlyList<string> Values(string parameter) =>
            parameter == Snail.MCP.Roslyn.Application.SessionReach.Parameter ? [Root] : [];
    }
}
