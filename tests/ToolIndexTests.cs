using System.Reflection;
using ModelContextProtocol.Server;
using Snail.MCP.Roslyn.Tools;

namespace Snail.MCP.Roslyn.Tests;

/// <summary>
/// The index of the tools. Thirty schemas are the largest thing this server puts in a window, and they are paid
/// for on every call, whether the step reaches for a tool or not. The index exists so that a role carries
/// a handful and asks for the rest by name.
/// </summary>
public sealed class ToolIndexTests
{
    private readonly ToolIndex _index = new();

    [Theory]
    [InlineData("where is this class used", "find_references")]
    [InlineData("does it compile", "get_diagnostics")]
    [InlineData("rename safely", "rename_symbol")]
    [InlineData("which tests cover this", "find_tests_for_symbol")]
    [InlineData("open the solution first", "load_solution")]
    public void A_question_in_plain_words_names_the_tool_that_answers_it(string question, string expected) =>
        Assert.Contains(expected, _index.Find(question, 3).Select(entry => entry.Name));

    /// <summary>Nothing matched: the whole catalog comes back, not nothing, since an empty answer reads as "no tool for it".</summary>
    [Fact]
    public void A_question_nothing_matches_returns_every_tool_and_says_so()
    {
        var answer = _index.Answer("превед медвед", 5);

        Assert.False(answer["isMatched"]!.GetValue<bool>());
        Assert.Equal(ToolIndex.All.Count, answer["tools"]!.AsArray().Count);
    }

    /// <summary>The search words live apart from the descriptions: otherwise every session would pay for them, and only a searcher reads them.</summary>
    [Fact]
    public void Every_tool_the_index_knows_other_words_for_is_a_tool_that_exists()
    {
        var real = ToolIndex.All.Select(entry => entry.Name).ToHashSet(StringComparer.Ordinal);

        Assert.All(ToolIndex.Meant(), name => Assert.Contains(name, real));
    }

    /// <summary>And the other way round: a tool without words of its own is found only by its name, which is almost never enough.</summary>
    [Fact]
    public void Every_tool_has_other_words_the_index_knows_it_by()
    {
        var known = ToolIndex.Meant().ToHashSet(StringComparer.Ordinal);

        Assert.All(ToolIndex.All, entry => Assert.Contains(entry.Name, known));
    }

    /// <summary>The index reads the tools themselves: a catalog written by hand beside them is wrong by the second release.</summary>
    [Fact]
    public void The_index_holds_exactly_the_tools_the_assembly_declares()
    {
        var declared = typeof(ToolIndex).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Count(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null);

        Assert.Equal(declared, ToolIndex.All.Count);
    }

    /// <summary>Parameters the SDK fills itself are not the model's to fill, and the index must not show them.</summary>
    [Fact]
    public void The_index_lists_only_the_parameters_the_model_fills()
    {
        var run = ToolIndex.All.Single(entry => entry.Name == "run_tools");

        Assert.Equal(["steps"], run.Parameters.Select(parameter => parameter.Name));
    }
}
