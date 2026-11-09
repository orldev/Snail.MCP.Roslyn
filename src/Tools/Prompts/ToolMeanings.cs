namespace Snail.MCP.Roslyn.Tools.Prompts;

/// <summary>The words a caller reaches for that a tool is not named with and its description need not carry.</summary>
/// <remarks>
/// They live here rather than in the descriptions because the descriptions are read by every session whether or not
/// anyone searches, and these are read only when someone asks <c>find_tool</c>. A guard test keeps the keys the tools the
/// server lists, and every tool among the keys.
/// </remarks>
internal static class ToolMeanings
{
    /// <summary>Tasks as a caller of this server puts them, shown in the description of find_tool's <c>task</c>.</summary>
    public static readonly IReadOnlyList<string> Examples = ["does it compile", "rename a symbol safely", "who calls this method"];

    /// <summary>The other words of each tool, by its name.</summary>
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>(StringComparer.Ordinal)
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
}
