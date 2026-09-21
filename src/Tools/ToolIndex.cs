using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Roslyn.Application;

namespace Snail.MCP.Roslyn.Tools;

/// <summary>Every tool of this server as an entry: what it is called, what it takes, what it is for.</summary>
/// <remarks>
/// Read from the tools themselves rather than written a second time beside them, because a catalog kept
/// by hand is wrong by the second release.
/// Thirty tools with their schemas are the largest thing this server puts in a window, and they are
/// paid for on every single call whether the step reaches for one or not. <c>find_tool</c> is what lets
/// a role carry a handful and ask for the rest by name: the words a caller searches by live here and
/// are read only when someone searches, instead of riding in every description of every session.
/// </remarks>
public sealed class ToolIndex
{
    /// <summary>Every tool, in the order the tool classes declare them.</summary>
    public static readonly IReadOnlyList<ToolEntry> All = Read();

    /// <summary>
    /// The words a caller reaches for that the tool is not named with and its description need not carry.
    /// </summary>
    /// <remarks>
    /// They live here rather than in the descriptions because the descriptions are read by every session
    /// whether or not anyone searches, and these are read only when someone does. A guard test keeps the
    /// keys the tools that exist.
    /// </remarks>
    private static readonly Dictionary<string, string> Meanings = new(StringComparer.Ordinal)
    {
        ["load_solution"] = "open start begin sln csproj project workspace first",
        ["unload_solution"] = "close release free drop forget memory",
        ["get_diagnostics"] = "errors warnings compile build broken does it compile red",
        ["read_file"] = "source contents show open lines text",
        ["get_solution_overview"] = "structure projects layout what is here map shape",
        ["check_architecture"] = "layers dependencies rules violations allowed depends",
        ["health_check"] = "memory restart alive state process",
        ["search_symbols"] = "find name class type method where is lookup",
        ["get_symbol_info"] = "about signature kind documentation declared",
        ["get_symbol_source"] = "body code of implementation text definition",
        ["find_references"] = "usages used who uses call sites mentions",
        ["find_implementations"] = "implements derived subclasses inheritors overrides",
        ["find_callers"] = "calls callers who calls invocations",
        ["get_type_hierarchy"] = "base derived inheritance tree parents children",
        ["get_generated_code"] = "source generator generated emitted razor designer",
        ["rename_symbol"] = "change name renaming identifier safely",
        ["rename_namespace"] = "move namespace change folder namespaces",
        ["move_type"] = "relocate another file split class into",
        ["organize_usings"] = "sort remove unused imports directives tidy",
        ["change_signature"] = "parameters reorder add remove argument overload",
        ["extract_component"] = "razor blazor split markup into component reuse",
        ["analyze_change_impact"] = "blast radius affected breaking touched what breaks",
        ["run_analyzers"] = "roslynator style rules analyzer issues quality",
        ["get_code_fixes"] = "fix suggestion quick action repair automatic",
        ["validate"] = "compiles would compile check before writing try",
        ["get_di_registrations"] = "container services registered injection resolve lifetime",
        ["find_unused_symbols"] = "dead unreachable never used delete safely",
        ["find_uncovered_symbols"] = "tests missing untested coverage",
        ["find_tests_for_symbol"] = "which tests cover exercising",
        ["run_script"] = "csharp script program loop compute custom query anything",
        ["run_tools"] = "several batch sequence steps together once round trip",
        ["find_tool"] = "search discover which tool name parameters how do i"
    };

    /// <summary>The tools that answer a question, best first; every one of them when none does.</summary>
    public IReadOnlyList<ToolEntry> Find(string? task, int limit) =>
    [
        .. All
            .Select(entry => (Entry: entry, Score: Vocabulary.Score(task ?? string.Empty, entry.Name, Meant(entry.Name), entry.Description)))
            .Where(scored => scored.Score > 0)
            .OrderByDescending(scored => scored.Score)
            .ThenBy(scored => scored.Entry.Name, StringComparer.Ordinal)
            .Take(Math.Clamp(limit, 1, All.Count))
            .Select(scored => scored.Entry)
    ];

    /// <summary>The answer <c>find_tool</c> gives: what matched, or the whole catalog when nothing did.</summary>
    public JsonObject Answer(string? task, int limit)
    {
        var found = Find(task, limit);

        return new JsonObject
        {
            ["task"] = task,
            ["isMatched"] = found.Count > 0,
            ["note"] = found.Count > 0 ? null : "nothing in this server's words matches; these are all of its tools",
            ["tools"] = new JsonArray([.. (found.Count > 0 ? found : All).Select(Describe)])
        };
    }

    /// <summary>The other words this tool is asked for by, or none when the index knows no others.</summary>
    public static string? Meant(string name) => Meanings.GetValueOrDefault(name);

    /// <summary>The tools the index knows other words for; a guard test keeps these the tools that exist.</summary>
    public static IEnumerable<string> Meant() => Meanings.Keys;

    private static JsonNode Describe(ToolEntry entry) => new JsonObject
    {
        ["name"] = entry.Name,
        ["description"] = entry.Description,
        ["parameters"] = new JsonArray([.. entry.Parameters.Select(parameter => (JsonNode)new JsonObject
        {
            ["name"] = parameter.Name,
            ["type"] = parameter.Type,
            ["isRequired"] = parameter.IsRequired,
            ["description"] = parameter.Description
        })])
    };

    private static IReadOnlyList<ToolEntry> Read() =>
    [
        .. typeof(ToolIndex).Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .Select(Entry)
    ];

    private static ToolEntry Entry(MethodInfo tool) => new(
        tool.GetCustomAttribute<McpServerToolAttribute>()!.Name!,
        tool.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty,
        [.. tool.GetParameters().Where(Fillable).Select(Argument)]);

    /// <summary>The parameters the model fills; the request and the cancellation token are the SDK's to fill.</summary>
    private static bool Fillable(ParameterInfo parameter) =>
        parameter.ParameterType != typeof(CancellationToken)
        && parameter.ParameterType != typeof(McpServer)
        && parameter.ParameterType != typeof(RequestContext<CallToolRequestParams>)
        && !parameter.ParameterType.IsAssignableTo(typeof(IProgress<WorkStep>));

    private static ToolArgument Argument(ParameterInfo parameter) => new(
        parameter.Name!,
        Shape(parameter.ParameterType),
        !parameter.HasDefaultValue,
        parameter.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty);

    /// <summary>The JSON shape of a parameter, as a caller filling it has to write it.</summary>
    private static string Shape(Type type) =>
        Nullable.GetUnderlyingType(type) is { } inner ? Shape(inner)
        : type == typeof(string) ? "string"
        : type == typeof(bool) ? "boolean"
        : type == typeof(int) ? "integer"
        : type == typeof(string[]) ? "string[]"
        : type == typeof(ToolStep[]) ? "step[]"
        : type.IsEnum ? string.Join(" | ", Enum.GetNames(type).Select(name => name.ToLowerInvariant()))
        : "object";
}

/// <summary>One tool as the index knows it.</summary>
public sealed record ToolEntry(string Name, string Description, IReadOnlyList<ToolArgument> Parameters);

/// <summary>One argument of a tool, with the words the model reads before filling it.</summary>
public sealed record ToolArgument(string Name, string Type, bool IsRequired, string Description);
