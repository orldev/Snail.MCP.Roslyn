namespace Snail.MCP.Roslyn.Tools.Prompts;

/// <summary>How to work with this server, delivered once at initialize; a wire contract, edited with caution.</summary>
public static class ServerGuidance
{
    // TODO: возможно сокраить
    public const string Instructions =
        """
        This server keeps a C# solution compiled in memory. Use it instead of reading files one by one,
        and instead of `dotnet build`, whenever you are working on C#.

        Start of a C# task: call load_solution once on the .sln or .csproj, keep the returned solutionId.
        Readiness 'Degraded' means restore has not run and empty answers are unreliable — say so rather than trusting them.

        To read a C# file: call read_file, not a plain file read. Same text, plus the file's diagnostics and
        every declaration with its identifier and use count; the identifier feeds find_references directly.
        This covers .razor and .cshtml too: mapped diagnostics, @code declarations, component tags and directives.

        After every edit: call get_diagnostics, not `dotnet build`. It answers in tens of milliseconds against the
        files as they are on disk, including files you added since loading. Reach for `dotnet build` only when
        packages or project files changed.

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
