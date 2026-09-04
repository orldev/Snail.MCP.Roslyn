namespace Snail.MCP.Roslyn.Tools.Prompts;

/// <summary>Tool text the model reads; a wire contract, edited with caution.</summary>
public static class ToolDescriptions
{
    public const string LoadSolution =
        "Load a C# solution or project and keep it compiled for later questions. "
        + "Returns the project list, counts of compiler errors and warnings, and a readiness state. "
        + "Readiness 'Degraded' means package restore has not run, so semantic answers under-report: "
        + "report that state instead of trusting an empty result. "
        + "The 'unloaded' list names .csproj files on disk that the solution does not include and that no answer covers. "
        + "Call this once per solution before any other tool.";

    public const string GetDiagnostics =
        "List compiler errors and warnings for a loaded solution, reflecting the files as they are on disk right now. "
        + "Call it right after editing to see what the change broke; it is cheap enough to use after every edit. "
        + "Identical diagnostics repeated across generated components are collapsed and counted in 'repeats'. "
        + "Truncation is always stated, so a short list is not the same as a complete one.";

    public const string ReadFile =
        "Read one file the way the compiler sees it: the numbered text, the diagnostics that live in it, "
        + "and every declaration it holds with the identifier the symbol tools expect and a count of its uses "
        + "across the solution. Prefer this over a plain file read for C#: same text, plus what text cannot show. "
        + "A .razor or .cshtml file of the solution comes back as a component view: diagnostics and @code "
        + "declarations mapped to their authored lines, component tags resolved to types, the directives "
        + "the file declares, and render-cost patterns under SNR ids. "
        + "A file outside the loaded solution comes back as text alone, and the answer says so.";

    public const string SearchSymbols =
        "Find symbols whose name contains the query and return the identifier every other tool expects. "
        + "This is the entry point: resolve a name to an identifier here, then pass that identifier on. "
        + "Never guess an identifier and never rename by bare name when more than one symbol matches.";

    public const string GetSymbolInfo =
        "Describe one symbol: kind, signature, accessibility, namespace, declaration site and documentation. "
        + "Address it either by the identifier from search_symbols or by a file position.";

    public const string GetSymbolSource =
        "Return the source text that declares one symbol, cut to its own span, so you can read a type or a method "
        + "without knowing which file holds it or how many lines it spans. "
        + "Address it by the identifier from search_symbols or by a file position. "
        + "Prefer this over reading a whole file when you only need one declaration.";

    public const string FindReferences =
        "List every place the compiler treats as a use of this symbol, so you can see what breaks before you touch it. "
        + "Covers overrides, interface implementations, nameof and documentation references, not text matches. "
        + "Each reference carries a kind — read, write, invocation, object_creation, type_argument and so on — "
        + "so fifty results can be judged without reading fifty files. "
        + "A use inside a .razor or .cshtml file is returned with kind 'markup' and the authored file, without a line. "
        + "Scope is the loaded solution: uses from projects outside it are invisible here and are not reported as absent.";

    public const string RenameSymbol =
        "Plan a solution-wide rename and return the edits for you to apply. This tool never writes to disk. "
        + "The compiler decides what a reference is, so overloads, overrides, interface implementations, nameof and "
        + "documentation references are all covered without text search. "
        + "Two parts of the answer matter equally. 'edits' is what the compiler guarantees. "
        + "'unhandled' lists text it will not touch and you must judge: string literals, comments, "
        + "container registrations by name, unrelated symbols sharing the name. "
        + "'conflicts' lists compiler errors the rename would introduce; a non-empty list means do not apply it. "
        + "A Blazor component is planned too: its .razor file rename and its markup tags arrive as edits, "
        + "with the rest of the markup under 'unhandled'. An @code member such as a [Parameter] renames as well: "
        + "attribute assignments in consumer markup are edited at the exact positions the compiler maps back. "
        + "If the name matches several symbols the answer carries 'candidates' and no edits: pick one and call again by id.";

    public const string ExtractComponent =
        "Plan the extraction of a markup block from a .razor file into a new component. This tool never writes: "
        + "it returns the new file's content as an edit and the page-side change as a compact replacement — "
        + "delete the given line range, put the replacement text in its place. "
        + "You supply the judgment — the line range, 'substitutions' renaming page-private names to parameter "
        + "names, the 'code' block declaring them, and the 'tag' that replaces the block; the tool supplies the "
        + "guarantees: the range must not cut an element apart (checked by the Razor parser), and the candidate "
        + "is compiled in context so 'unresolved' names every identifier that stops resolving outside the page. "
        + "Call it first without substitutions to learn the leaks; while 'unresolved' is non-empty the plan "
        + "carries no edits. Then call again with the full judgment, apply, and run get_diagnostics.";

    public const string GetSolutionOverview =
        "Describe the whole solution in one budgeted answer: project graph, sizes, compiler errors, "
        + "the types the rest of the code names most often, and the structural breaches found. "
        + "Call this before proposing a restructuring, instead of reading files one by one. "
        + "Analysis always covers the whole solution; only the answer is budgeted, and truncation is stated.";

    public const string CheckArchitecture =
        "List structural breaches: namespaces that do not match their folder, namespaces that depend on each other in a cycle, "
        + "projects that do, and markup blocks the solution repeats — a repeated block differing only in values "
        + "is a candidate to extract into a component. "
        + "Every finding is a claim about the code checkable at the file and line given. "
        + "Whether a breach matters is a judgement this tool does not make.";

    public const string HealthCheck =
        "Report whether the server itself is in a state where its answers can be trusted: readiness, project and document counts, "
        + "how long a full diagnostic pass takes, memory in use, how many solutions are loaded, and whether scripting is enabled. "
        + "Call this when an answer looks suspiciously empty or suspiciously fast.";

    public const string FindImplementations =
        "List what implements or derives from this symbol: implementations of an interface or interface member, "
        + "derived classes, derived interfaces, overriding members. "
        + "These are compiler edges, invisible to text search, and they are what a signature change breaks.";

    public const string FindCallers =
        "List the methods that call this one, as symbols rather than as locations, "
        + "so a call graph can be walked upwards without reading files.";

    public const string GetTypeHierarchy =
        "Show where a type sits: what it inherits, every interface it carries, and everything that derives from it. "
        + "One call replaces a chain of find_references.";

    public const string GetGeneratedCode =
        "List what the source generators contributed to a project, so a symbol with no declaration in source can be traced. "
        + "Generated files are rebuilt on every compile: editing them changes nothing, "
        + "so change the input the generator reads instead.";

    public const string AnalyzeChangeImpact =
        "Measure what depends on a symbol before anyone changes it: references grouped by project, "
        + "the overrides and interface implementations a shape change would break, "
        + "and the text occurrences the compiler will not protect. "
        + "Call this before a rename or a signature change, not after.";

    public const string RunAnalyzers =
        "Run the analyzers over the solution and report what they find, with the category that explains why it matters. "
        + "Use category 'Performance' for the known-slow patterns worth replacing. "
        + "This costs seconds, not milliseconds: use get_diagnostics for the after-every-edit loop. "
        + "If a project references no analyzers, the ones shipped with this server are used and the answer says so.";

    public const string GetCodeFixes =
        "Return the repairs the compiler's own providers offer for a diagnostic, as edits for you to apply. "
        + "This tool never writes to disk. A fix that compiles is not automatically a fix that is right.";

    public const string Validate =
        "Compile a file's proposed contents in the context of the solution, before writing anything to disk. "
        + "Answers what the change would break and what it would repair. "
        + "Use this when you are about to write a file and the cost of being wrong is a broken build.";

    public const string GetDiRegistrations =
        "List the dependency injection registrations found in source: service, implementation and lifetime, with the line that declares them. "
        + "Also names the interfaces that are implemented but registered nowhere. "
        + "A registration written as a string or resolved by reflection is invisible to rename_symbol: "
        + "this is where you check what a rename left behind.";

    public const string FindUnusedSymbols =
        "List declarations that no code in the loaded solution references. "
        + "That is not the same as dead: reflection, injection by name and serialisation are invisible to the compiler. "
        + "Blazor markup is counted through the generated code, so an unused [Parameter] or @inject appears here "
        + "with its authored file and line. "
        + "Treat every finding as a candidate to verify, never as a licence to delete.";

    public const string FindUncoveredSymbols =
        "List public declarations that no test project in this solution references. "
        + "Referencing is not testing, but zero references from tests is a hard signal that text search cannot produce.";

    public const string FindTestsForSymbol =
        "List the test methods that reach this symbol, so you know what will catch a change to it before you make one.";

    public const string RenameNamespace =
        "Plan a namespace rename across the solution and return the edits for you to apply. This tool never writes to disk. "
        + "With updateFolders the file moves that realign the folders with the new namespace are included. "
        + "Old and new must have the same number of segments; otherwise no edits are proposed and the textual picture is returned instead.";

    public const string MoveType =
        "Plan moving a type to another file, another namespace, or both, and return the edits for you to apply. "
        + "This tool never writes to disk. The type keeps its name, so references stay valid unless the namespace changed; "
        + "'conflicts' lists any compiler error the move would introduce.";

    public const string OrganizeUsings =
        "Plan the removal of using directives the compiler reports as unnecessary and sort the rest, System first. "
        + "This tool never writes to disk. Knowing which directive is unnecessary needs semantics, which is why this is not a text edit.";

    public const string ChangeSignature =
        "Plan a change to a method signature and return the edit for the declaration. This tool never writes to disk. "
        + "The compiler offers no cascading signature change, so every call site that stops compiling is listed under 'conflicts' — "
        + "that list is complete and is your work list.";

    public const string RunScript =
        "Evaluate a C# script against the loaded solution and return its value and printed output. "
        + "Use it for the question no fixed tool anticipates: one pass over the whole solution, aggregated the way you need. "
        + "Available globals: Solution, Compilations, Print(value), Types(pattern), Sym(id), Refs(symbol), Impls(symbol), Callers(symbol). "
        + "The script cannot change the code: the workspace is an in-memory snapshot and edits to it are discarded.";

    public const string UnloadSolution =
        "Release a loaded solution and the memory its compilations hold. "
        + "Use it when you are done with one solution and about to work on another; loading again is cheap.";
}
