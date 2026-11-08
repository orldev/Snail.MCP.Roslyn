using ModelContextProtocol.Protocol;
using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Snail.MCP.Roslyn.Tools;
using Snail.MCP.Roslyn.Tools.Prompts;

namespace Snail.MCP.Roslyn.Tests;

public sealed class ToolContractTests
{
    private static readonly MethodInfo[] Tools =
    [
        .. typeof(SolutionTools).Assembly
            .GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
    ];

    private static readonly string[] Descriptions =
    [
        .. typeof(ToolDescriptions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
    ];

    [Fact]
    public void ToolNames_EveryTool_AreSnakeCase()
    {
        Assert.NotEmpty(Tools);
        Assert.All(Tools, tool =>
        {
            var name = tool.GetCustomAttribute<McpServerToolAttribute>()!.Name;

            Assert.NotNull(name);
            Assert.Matches("^[a-z][a-z0-9_]*$", name);
        });
    }

    [Fact]
    public void ToolDescription_EveryTool_ComesFromToolDescriptions()
    {
        Assert.All(Tools, tool =>
        {
            var description = tool.GetCustomAttribute<DescriptionAttribute>()?.Description;

            Assert.False(string.IsNullOrWhiteSpace(description));
            Assert.Contains(description, Descriptions);
        });
    }

    [Fact]
    public void ToolParameters_EveryNonCancellationParameter_CarryDescription()
    {
        Assert.All(Tools, tool =>
            Assert.All(
                tool.GetParameters().Where(IsAsked),
                parameter => Assert.False(
                    string.IsNullOrWhiteSpace(parameter.GetCustomAttribute<DescriptionAttribute>()?.Description),
                    $"{tool.Name}.{parameter.Name} has no description")));
    }

    /// <summary>Parameters the caller fills in. The cancellation token, the progress sink and the request
    /// itself are supplied by the server, never appear in the schema, and so have nobody to describe
    /// themselves to.</summary>
    private static bool IsAsked(ParameterInfo parameter) =>
        parameter.ParameterType != typeof(CancellationToken)
        && parameter.ParameterType != typeof(IProgress<ProgressNotificationValue>)
        && parameter.ParameterType != typeof(RequestContext<CallToolRequestParams>);

    [Fact]
    public void RefactorToolDescriptions_EveryTool_PromiseNeverWrites()
    {
        var refactorings = Tools.Where(tool => tool.DeclaringType == typeof(RefactorTools));

        Assert.NotEmpty(refactorings);
        Assert.All(refactorings, tool => Assert.Contains(
            "never writes",
            tool.GetCustomAttribute<DescriptionAttribute>()!.Description,
            StringComparison.Ordinal));
    }

    [Fact]
    public void ToolSurface_AgainstDeclaredCatalogue_MatchesExactly()
    {
        string[] declared =
        [
            "analyze_change_impact", "change_signature", "check_architecture", "extract_component", "find_callers", "find_implementations",
            "find_references", "find_tests_for_symbol", "find_uncovered_symbols", "find_unused_symbols",
            "get_code_fixes", "get_di_registrations", "get_diagnostics", "get_generated_code", "get_solution_overview",
            "get_symbol_info", "get_symbol_source", "get_type_hierarchy", "health_check", "load_solution",
            "move_type", "organize_usings", "read_file", "rename_namespace", "rename_symbol", "run_analyzers", "run_script",
            "search_symbols", "unload_solution", "validate"
        ];

        string[] actual =
        [
            .. Tools.Select(tool => tool.GetCustomAttribute<McpServerToolAttribute>()!.Name!).Order(StringComparer.Ordinal)
        ];

        Assert.Equal(declared, actual);
    }

    [Fact]
    public void EditReturningToolDescriptions_EveryTool_PromiseNeverWrites()
    {
        string[] editing = ["rename_symbol", "rename_namespace", "move_type", "organize_usings", "change_signature", "get_code_fixes"];

        Assert.All(
            Tools.Where(tool => editing.Contains(tool.GetCustomAttribute<McpServerToolAttribute>()!.Name)),
            tool => Assert.Contains(
                "never writes",
                tool.GetCustomAttribute<DescriptionAttribute>()!.Description,
                StringComparison.Ordinal));
    }

    /// <summary>The mechanics shared by the whole fleet are Snail.Sdk's, spelled alike in every server.</summary>
    /// <remarks>
    /// find_tool, run_tools and run_program are not about the solution but about how ANY server is asked
    /// what it can do, and how several of its tools are spent in one call. The platform calls them as
    /// "server.name", and Snail.Sdk registers them (<c>WithToolDiscovery</c>, <c>WithToolSequences</c>) so that
    /// the same question is asked of every server in one way: a copy of one declared here, or a tool named after
    /// one, would be a second way. That the server lists them is checked on the wire (<see cref="DiscoveryTests"/>).
    /// </remarks>
    [Fact]
    public void The_machinery_of_the_fleet_is_the_sdk_s_and_no_tool_of_this_server_copies_it()
    {
        string[] machinery = ["find_tool", "run_program", "run_tools"];

        var names = Tools.Select(tool => tool.GetCustomAttribute<McpServerToolAttribute>()!.Name!).ToList();

        Assert.Empty(names.Intersect(machinery));
        Assert.DoesNotContain(names, name => machinery.Any(known => name.EndsWith("_" + known, StringComparison.Ordinal)));
    }
}
