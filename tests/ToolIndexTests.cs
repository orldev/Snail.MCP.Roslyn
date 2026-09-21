using System.Reflection;
using ModelContextProtocol.Server;
using Snail.MCP.Roslyn.Tools;

namespace Snail.MCP.Roslyn.Tests;

/// <summary>
/// Указатель по инструментам. Тридцать схем — самое большое, что этот сервер кладёт в окно, и платятся
/// они на каждом вызове, дотянулся шаг до инструмента или нет. Указатель существует, чтобы роль носила
/// горсть, а остальное спрашивала по имени.
/// </summary>
public sealed class ToolIndexTests
{
    private readonly ToolIndex index = new();

    [Theory]
    [InlineData("where is this class used", "find_references")]
    [InlineData("does it compile", "get_diagnostics")]
    [InlineData("rename safely", "rename_symbol")]
    [InlineData("which tests cover this", "find_tests_for_symbol")]
    [InlineData("open the solution first", "load_solution")]
    public void A_question_in_plain_words_names_the_tool_that_answers_it(string question, string expected) =>
        Assert.Contains(expected, index.Find(question, 3).Select(entry => entry.Name));

    /// <summary>Ничего не совпало — отдаётся весь каталог, а не пустота: пустой ответ читается как «нечем».</summary>
    [Fact]
    public void A_question_nothing_matches_returns_every_tool_and_says_so()
    {
        var answer = index.Answer("превед медвед", 5);

        Assert.False(answer["isMatched"]!.GetValue<bool>());
        Assert.Equal(ToolIndex.All.Count, answer["tools"]!.AsArray().Count);
    }

    /// <summary>Слова для поиска живут отдельно от описаний — иначе их платил бы каждый сеанс, а читает их только ищущий.</summary>
    [Fact]
    public void Every_tool_the_index_knows_other_words_for_is_a_tool_that_exists()
    {
        var real = ToolIndex.All.Select(entry => entry.Name).ToHashSet(StringComparer.Ordinal);

        Assert.All(ToolIndex.Meant(), name => Assert.Contains(name, real));
    }

    /// <summary>И наоборот: инструмент без своих слов находится только по названию, что почти всегда мало.</summary>
    [Fact]
    public void Every_tool_has_other_words_the_index_knows_it_by()
    {
        var known = ToolIndex.Meant().ToHashSet(StringComparer.Ordinal);

        Assert.All(ToolIndex.All, entry => Assert.Contains(entry.Name, known));
    }

    /// <summary>Указатель читает сами инструменты: каталог, писанный рядом руками, врёт со второго выпуска.</summary>
    [Fact]
    public void The_index_holds_exactly_the_tools_the_assembly_declares()
    {
        var declared = typeof(ToolIndex).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Count(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null);

        Assert.Equal(declared, ToolIndex.All.Count);
    }

    /// <summary>Параметры, которые подставляет сам SDK, модель не заполняет и видеть их в указателе не должна.</summary>
    [Fact]
    public void The_index_lists_only_the_parameters_the_model_fills()
    {
        var run = ToolIndex.All.Single(entry => entry.Name == "run_tools");

        Assert.Equal(["steps"], run.Parameters.Select(parameter => parameter.Name));
    }
}
