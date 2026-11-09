using ModelContextProtocol.Client;
using Snail.MCP.Roslyn.Tools.Prompts;

namespace Snail.MCP.Roslyn.Tests;

/// <summary>
/// find_tool over this server's tools. Thirty schemas are the largest thing this server puts in a window, and they are
/// paid for on every call, whether the step reaches for a tool or not. find_tool exists so that a role carries a handful
/// and asks for the rest in words; the ranking is Snail.Sdk's, the words this server's (<see cref="ToolMeanings"/>).
/// </summary>
public sealed class DiscoveryTests(DiscoveryTests.Server server) : IClassFixture<DiscoveryTests.Server>
{
    [Theory]
    [InlineData("where is this class used", "find_references")]
    [InlineData("does it compile", "get_diagnostics")]
    [InlineData("rename safely", "rename_symbol")]
    [InlineData("which tests cover this", "find_tests_for_symbol")]
    [InlineData("open the solution first", "load_solution")]
    public async Task A_question_in_plain_words_names_the_tool_that_answers_it(string question, string expected)
    {
        var answer = await server.Client.CallToolAsync("find_tool", new Dictionary<string, object?> { ["task"] = question, ["limit"] = 3 });

        Assert.True(answer.IsOk(), answer.Text());
        Assert.Contains(expected, Named(answer.Data()!["tools"]!));
    }

    /// <summary>Nothing matched: the whole catalog comes back, not nothing, since an empty answer reads as "no tool for it".</summary>
    [Fact]
    public async Task A_question_nothing_matches_returns_every_tool_and_says_so()
    {
        var listed = await server.Client.ListToolsAsync();

        var answer = await server.Client.CallToolAsync("find_tool", new Dictionary<string, object?> { ["task"] = "превед медвед" });

        Assert.False(answer.Data()!["isMatched"]!.GetValue<bool>());
        Assert.Equal(listed.Count, answer.Data()!["tools"]!.AsArray().Count);
    }

    /// <summary>The examples find_tool shows under <c>task</c> are this server's own words, and each of them finds a tool.</summary>
    [Fact]
    public async Task Every_example_shown_to_the_caller_is_a_task_that_finds_a_tool()
    {
        var listed = await server.Client.ListToolsAsync();
        var described = listed.Single(tool => tool.Name == "find_tool").ProtocolTool.InputSchema.GetRawText();

        foreach (var example in ToolMeanings.Examples)
        {
            var answer = await server.Client.CallToolAsync("find_tool", new Dictionary<string, object?> { ["task"] = example, ["limit"] = 3 });

            Assert.Contains(example, described, StringComparison.Ordinal);
            Assert.True(answer.Data()!["isMatched"]!.GetValue<bool>(), example);
        }
    }

    /// <summary>The fleet's ways in are listed beside the server's own tools, under the names every server spells alike.</summary>
    [Fact]
    public async Task The_ways_in_of_the_fleet_are_listed()
    {
        var listed = await server.Client.ListToolsAsync();

        Assert.Contains("find_tool", listed.Select(tool => tool.Name));
        Assert.Contains("run_tools", listed.Select(tool => tool.Name));
    }

    /// <summary>The search words live apart from the descriptions, keyed by tool: a key no tool answers to is a word nobody finds.</summary>
    [Fact]
    public async Task Every_tool_the_server_knows_other_words_for_is_a_tool_it_lists()
    {
        var listed = (await server.Client.ListToolsAsync()).Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);

        Assert.All(ToolMeanings.All.Keys, name => Assert.Contains(name, listed));
    }

    /// <summary>And the other way round: a tool without words of its own is found only by its name, which is almost never enough.</summary>
    [Fact]
    public async Task Every_tool_it_lists_has_other_words_it_is_known_by()
    {
        var listed = await server.Client.ListToolsAsync();

        Assert.All(listed, tool => Assert.Contains(tool.Name, ToolMeanings.All.Keys));
    }

    private static IEnumerable<string> Named(System.Text.Json.Nodes.JsonNode tools) =>
        tools.AsArray().Select(tool => tool!["name"]!.GetValue<string>());

    /// <summary>One server over stdio for the class: each question is a call, not a process.</summary>
    public sealed class Server : IAsyncLifetime
    {
        public McpClient Client { get; private set; } = null!;

        public async Task InitializeAsync() =>
            Client = await ServerProgram.StartAsync("Snail.MCP.Roslyn", "SNAIL_ROSLYN_SERVER");

        public async Task DisposeAsync() => await Client.DisposeAsync();
    }
}
