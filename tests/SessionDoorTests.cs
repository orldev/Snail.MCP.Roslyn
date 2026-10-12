namespace Snail.MCP.Roslyn.Tests;

/// <summary>
/// Two doors a path gets in through: loading a solution by its path, and everything else by its id.
/// The second is quieter and so more dangerous: the id is computed from the path deterministically, so in a shared
/// container another session's id need not be guessed, it can be computed.
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

    private readonly string _mine = Directory.CreateTempSubdirectory("roslyn-door-mine").FullName;

    private readonly string _theirs = Directory.CreateTempSubdirectory("roslyn-door-theirs").FullName;

    [Fact]
    public async Task LoadSolution_APathOutsideTheSession_IsRefusedAndSaysWhere()
    {
        await WriteAsync(_theirs, "Theirs");

        using var fixture = new AnalysisFixture([], new Address(_mine));

        var report = await fixture.Host.LoadAsync(_theirs, CancellationToken.None);

        var said = string.Join(" ", report.Notes);

        Assert.Contains("outside this session", said, StringComparison.Ordinal);
        Assert.Contains(_mine, said, StringComparison.Ordinal);
    }

    /// <summary>
    /// A solution loaded by another session stays in the process's shared cache — and is not reachable by its id.
    /// Without this door one worker would read the files and diagnostics of another's task, knowing only its path.
    /// </summary>
    [Fact]
    public async Task ASolutionLoadedByAnotherSession_IsNotReachableByItsId()
    {
        await WriteAsync(_theirs, "Theirs");

        var address = new Address(_theirs);
        using var fixture = new AnalysisFixture([], address);

        var loaded = await fixture.Host.LoadAsync(_theirs, CancellationToken.None);
        Assert.NotEmpty(loaded.Id);

        address.Root = _mine;

        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Diagnostics.ReadAsync(loaded.Id, null, IssueSeverity.Error, 10, false, CancellationToken.None));

        Assert.Contains("another session", refused.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        Directory.Delete(_mine, recursive: true);
        Directory.Delete(_theirs, recursive: true);
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
