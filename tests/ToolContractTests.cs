using System.ComponentModel;
using System.Reflection;
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
                tool.GetParameters().Where(parameter => parameter.ParameterType != typeof(CancellationToken)),
                parameter => Assert.False(
                    string.IsNullOrWhiteSpace(parameter.GetCustomAttribute<DescriptionAttribute>()?.Description),
                    $"{tool.Name}.{parameter.Name} has no description")));
    }

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
}
