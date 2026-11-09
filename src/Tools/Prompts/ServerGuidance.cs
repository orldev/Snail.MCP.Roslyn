namespace Snail.MCP.Roslyn.Tools.Prompts;

/// <summary>How to work with this server, delivered once at initialize; a wire contract, edited with caution.</summary>
internal static class ServerGuidance
{
    public const string Instructions =
        """
        This server keeps a C# solution compiled in memory. Use it instead of reading files one by one,
        and instead of `dotnet build`, whenever you are working on C#.

        Do not know which tool: call find_tool with the task in words. Several questions that do not depend on
        each other's answers: call run_tools once with them as steps, instead of a round trip each.

        Every answer is JSON: {"ok":true,"data":…} with the tool's answer in data. A failed call is flagged isError
        and answers ok false, an error, a hint when there is something to do and a type (notLoaded: call
        load_solution): read them and change the call instead of repeating it.

        Start of a C# task: call load_solution once on the .sln or .csproj, keep data.id as the solutionId.
        Readiness 'Degraded' means restore has not run and empty answers are unreliable — say so rather than trusting them.

        Addressing a symbol: give symbolId from search_symbols, or, where the tool takes one, a bare name that
        matches exactly one symbol, or file with line and column. Prefer the id; a bare name is a guess.

        To read a C# file: call read_file, not a plain file read. Same text, plus the file's diagnostics and
        every declaration with its identifier and use count; the identifier feeds find_references directly.
        This covers .razor and .cshtml too: mapped diagnostics, @code declarations, component tags and directives.

        After every edit: call get_diagnostics, not `dotnet build`. It answers in tens of milliseconds against the
        files as they are on disk, including files you added since loading. Reach for `dotnet build` only when
        packages or project files changed. Where warnings are errors, pass analyzers=true with the file as scope:
        the project's analyzers fail such a build on code that compiles.

        Before writing a file where a broken build would cost you: call validate with the proposed contents.
        It compiles them in context without touching the disk.

        Before renaming or restructuring: call analyze_change_impact, then rename_symbol, rename_namespace or move_type.
        These return edits and never write. Apply `edits` as given. Read `unhandled` yourself — string literals,
        comments and container registrations by name are not references and will not be rewritten. If `conflicts`
        is not empty, do not apply the plan.

        Before proposing a restructuring: call get_solution_overview once instead of reading files to build a picture.

        For a question no tool covers: run_script, one pass over the whole solution.

        Notes on an answer are part of it. An empty result carrying a note means "unknown", never "none".
        """;
}
