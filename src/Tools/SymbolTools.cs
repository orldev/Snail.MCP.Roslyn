using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Roslyn.Tools.Prompts;

namespace Snail.MCP.Roslyn.Tools;

[McpServerToolType]
public sealed class SymbolTools(
    ISymbolLookup symbols,
    IReferenceFinder references,
    IHierarchyExplorer hierarchy,
    IGeneratedCodeReader generated)
{
    private const string NoSymbol = "No symbol is there: the id, or the file, line and column, resolve to nothing in the loaded solution.";

    private const string NoSymbolHint = "Take the id from search_symbols, or point line and column at the symbol's name itself.";

    private const string NoSource = "No source is there: nothing resolves, or the symbol is declared in a referenced assembly rather than in the solution.";

    private const string NoSourceHint = "Take the id from search_symbols; get_symbol_info describes a symbol of a referenced assembly.";

    private const string NoType = "No type is there: the id, name or position resolves to nothing, or to a symbol that is not a type.";

    private const string NoTypeHint = "Name a class, struct, interface or record: its id from search_symbols, its exact name, or the position of its name.";

    [McpServerTool(Name = "search_symbols", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.SearchSymbols)]
    public Task<CallToolResult> SearchSymbols(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        [Description("Part of the symbol name; matching ignores case.")]
        string query,
        CancellationToken cancellationToken,
        [Description("Narrow to one declaration family: Any, Type, Method, Property, Field or Event.")]
        SymbolKindFilter kind = SymbolKindFilter.Any,
        [Description("Maximum candidates to return.")]
        int limit = 30) =>
        symbols.SearchAsync(solutionId, query, kind, limit, cancellationToken)
            .AnsweredAsync(RoslynJson.Default.IReadOnlyListSymbolMatch);

    [McpServerTool(Name = "get_symbol_info", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.GetSymbolInfo)]
    public Task<CallToolResult> GetSymbolInfo(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken,
        [Description(ParameterDescriptions.SymbolId)]
        string? symbolId = null,
        [Description(ParameterDescriptions.File)]
        string? file = null,
        [Description(ParameterDescriptions.Line)]
        int line = 0,
        [Description(ParameterDescriptions.Column)]
        int column = 0) =>
        symbols.DescribeAsync(solutionId, new SymbolQuery(symbolId, null, file, line, column), cancellationToken)
            .FoundAsync(RoslynJson.Default.SymbolDetails, NoSymbol, NoSymbolHint);

    [McpServerTool(Name = "get_symbol_source", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.GetSymbolSource)]
    public Task<CallToolResult> GetSymbolSource(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken,
        [Description(ParameterDescriptions.SymbolId)]
        string? symbolId = null,
        [Description(ParameterDescriptions.File)]
        string? file = null,
        [Description(ParameterDescriptions.Line)]
        int line = 0,
        [Description(ParameterDescriptions.Column)]
        int column = 0) =>
        symbols.ReadAsync(solutionId, new SymbolQuery(symbolId, null, file, line, column), cancellationToken)
            .FoundAsync(RoslynJson.Default.SymbolSource, NoSource, NoSourceHint);

    [McpServerTool(Name = "find_references", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.FindReferences)]
    public Task<CallToolResult> FindReferences(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken,
        [Description(ParameterDescriptions.SymbolId)]
        string? symbolId = null,
        [Description(ParameterDescriptions.File)]
        string? file = null,
        [Description(ParameterDescriptions.Line)]
        int line = 0,
        [Description(ParameterDescriptions.Column)]
        int column = 0,
        [Description("Maximum references to return.")]
        int limit = 100) =>
        references.FindAsync(solutionId, new SymbolQuery(symbolId, null, file, line, column), limit, cancellationToken)
            .AnsweredAsync(RoslynJson.Default.ReferencePage);

    [McpServerTool(Name = "find_implementations", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.FindImplementations)]
    public Task<CallToolResult> FindImplementations(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken,
        [Description(ParameterDescriptions.SymbolId)]
        string? symbolId = null,
        [Description(ParameterDescriptions.SymbolName)]
        string? name = null,
        [Description(ParameterDescriptions.File)]
        string? file = null,
        [Description(ParameterDescriptions.Line)]
        int line = 0,
        [Description(ParameterDescriptions.Column)]
        int column = 0,
        [Description("Maximum results to return.")]
        int limit = 50) =>
        hierarchy.ImplementationsAsync(solutionId, new SymbolQuery(symbolId, name, file, line, column), limit, cancellationToken)
            .AnsweredAsync(RoslynJson.Default.IReadOnlyListSymbolMatch);

    [McpServerTool(Name = "find_callers", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.FindCallers)]
    public Task<CallToolResult> FindCallers(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken,
        [Description(ParameterDescriptions.SymbolId)]
        string? symbolId = null,
        [Description(ParameterDescriptions.SymbolName)]
        string? name = null,
        [Description(ParameterDescriptions.File)]
        string? file = null,
        [Description(ParameterDescriptions.Line)]
        int line = 0,
        [Description(ParameterDescriptions.Column)]
        int column = 0,
        [Description("Maximum results to return.")]
        int limit = 50) =>
        hierarchy.CallersAsync(solutionId, new SymbolQuery(symbolId, name, file, line, column), limit, cancellationToken)
            .AnsweredAsync(RoslynJson.Default.IReadOnlyListSymbolMatch);

    [McpServerTool(Name = "get_type_hierarchy", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.GetTypeHierarchy)]
    public Task<CallToolResult> GetTypeHierarchy(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken,
        [Description(ParameterDescriptions.SymbolId)]
        string? symbolId = null,
        [Description(ParameterDescriptions.TypeName)]
        string? name = null,
        [Description(ParameterDescriptions.File)]
        string? file = null,
        [Description(ParameterDescriptions.Line)]
        int line = 0,
        [Description(ParameterDescriptions.Column)]
        int column = 0) =>
        hierarchy.HierarchyAsync(solutionId, new SymbolQuery(symbolId, name, file, line, column), cancellationToken)
            .FoundAsync(RoslynJson.Default.TypeHierarchy, NoType, NoTypeHint);

    [McpServerTool(Name = "get_generated_code", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.GetGeneratedCode)]
    public Task<CallToolResult> GetGeneratedCode(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Project name or part of it; the first C# project when omitted.")]
        string? project = null,
        [Description("Part of the generated file name, to narrow the list.")]
        string? hintName = null,
        [Description("Include the generated text itself, not just the file list.")]
        bool includeText = false,
        [Description("Maximum files to return.")]
        int limit = 20) =>
        generated.ReadAsync(solutionId, project, hintName, includeText, limit, cancellationToken)
            .AnsweredAsync(RoslynJson.Default.GeneratedCodePage);
}
