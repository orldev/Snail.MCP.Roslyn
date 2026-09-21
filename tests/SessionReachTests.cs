using Microsoft.Extensions.Options;
using Snail.MCP.Roslyn.Application;
using Snail.MCP.Roslyn.Configuration;

namespace Snail.MCP.Roslyn.Tests;

/// <summary>
/// Насколько далеко дотягивается сессия, стоящая перед сервером. Один процесс на одного вызывающего
/// этого вопроса не знал: что попросили загрузить, то и его. Контейнер, общий на машину, — другое
/// животное: рабочая копия у каждого воркера своя, и весь смысл в том, чтобы никто не дотянулся до чужой.
/// </summary>
public sealed class SessionReachTests
{
    [Fact]
    public void A_session_that_named_a_root_reaches_inside_it_and_nowhere_else()
    {
        var reach = Opened(at: "/workspaces/task-1");

        Assert.True(reach.Allows("/workspaces/task-1/src/App.csproj"));
        Assert.False(reach.Allows("/workspaces/task-2/src/App.csproj"));
    }

    /// <summary>Сосед, чьё имя лишь начинается так же, — классический способ выйти из клетки.</summary>
    [Fact]
    public void A_sibling_whose_name_merely_begins_the_same_way_is_not_inside() =>
        Assert.False(Opened(at: "/workspaces/task-1").Allows("/workspaces/task-1-secrets/App.csproj"));

    /// <summary>Сервер, поднятый ради одного вызывающего, не назовёт корня — и это не дыра, а его устройство.</summary>
    [Fact]
    public void A_session_that_named_nothing_reaches_wherever_the_process_can() =>
        Assert.True(Opened(at: null).Allows("/anywhere/at/all.csproj"));

    /// <summary>Настроенный корень стоит там, где адреса нет: так запертый программой сервер и запирается.</summary>
    [Fact]
    public void A_configured_root_stands_when_the_address_names_none()
    {
        var reach = Opened(at: null, configured: "/workspaces");

        Assert.True(reach.Allows("/workspaces/task-1/App.csproj"));
        Assert.False(reach.Allows("/etc/passwd"));
    }

    /// <summary>Симлинк внутри корня — самый дешёвый способ выйти за него, поэтому ссылки раскрываются.</summary>
    [Fact]
    public void A_symlink_that_leads_out_of_the_root_does_not_pass_as_a_path_inside_it()
    {
        var one = Directory.CreateTempSubdirectory("roslyn-reach-one").FullName;
        var two = Directory.CreateTempSubdirectory("roslyn-reach-two").FullName;

        try
        {
            var door = Path.Combine(one, "out");
            Directory.CreateSymbolicLink(door, two);

            Assert.False(Opened(at: one).Allows(Path.Combine(door, "App.csproj")));
        }
        finally
        {
            Directory.Delete(one, recursive: true);
            Directory.Delete(two, recursive: true);
        }
    }

    /// <summary>
    /// Ссылка ПОСЕРЕДИНЕ пути: ResolveLinkTarget отвечает только про последний сегмент, поэтому
    /// /копия/out/решение.sln раскрывался сам в себя и уходил за корень. Нашлось на filesystem-сервере.
    /// </summary>
    [Fact]
    public void A_symlink_in_the_middle_of_the_path_does_not_pass_as_a_path_inside_the_root()
    {
        var one = Directory.CreateTempSubdirectory("roslyn-reach-mid-one").FullName;
        var two = Directory.CreateTempSubdirectory("roslyn-reach-mid-two").FullName;

        try
        {
            File.WriteAllText(Path.Combine(two, "Other.sln"), string.Empty);
            Directory.CreateSymbolicLink(Path.Combine(one, "out"), two);

            Assert.False(Opened(at: one).Allows(Path.Combine(one, "out", "Other.sln")));
        }
        finally
        {
            Directory.Delete(one, recursive: true);
            Directory.Delete(two, recursive: true);
        }
    }

    /// <summary>Отказ называет корень: иначе человек читает «нельзя» и не знает, куда можно.</summary>
    [Fact]
    public void The_refusal_names_where_the_session_does_work() =>
        Assert.Contains("/workspaces/task-1", Opened(at: "/workspaces/task-1").Refusal("/elsewhere"), StringComparison.Ordinal);

    private static SessionReach Opened(string? at, string? configured = null) =>
        new(Options.Create(new WorkspaceOptions { SessionRoots = configured is null ? [] : [configured] }),
            new Address(at));

    private sealed class Address(string? root) : ISessionAddress
    {
        public IReadOnlyList<string> Values(string parameter) =>
            parameter == SessionReach.Parameter && root is not null ? [root] : [];
    }
}
