using System.ComponentModel;
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
    [McpServerTool(Name = "search_symbols", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.SearchSymbols)]
    public Task<IReadOnlyList<SymbolMatch>> SearchSymbols(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        [Description("Part of the symbol name; matching ignores case.")]
        string query,
        CancellationToken cancellationToken,
        [Description("Narrow to one declaration family: Any, Type, Method, Property, Field or Event.")]
        SymbolKindFilter kind = SymbolKindFilter.Any,
        [Description("Maximum candidates to return.")]
        int limit = 30) =>
        symbols.SearchAsync(solutionId, query, kind, limit, cancellationToken);

    [McpServerTool(Name = "get_symbol_info", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.GetSymbolInfo)]
    public Task<SymbolDetails?> GetSymbolInfo(
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
        symbols.DescribeAsync(solutionId, new SymbolQuery(symbolId, null, file, line, column), cancellationToken);

    [McpServerTool(Name = "get_symbol_source", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.GetSymbolSource)]
    public Task<SymbolSource?> GetSymbolSource(
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
        symbols.ReadAsync(solutionId, new SymbolQuery(symbolId, null, file, line, column), cancellationToken);

    [McpServerTool(Name = "find_references", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.FindReferences)]
    public Task<ReferencePage> FindReferences(
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
        references.FindAsync(solutionId, new SymbolQuery(symbolId, null, file, line, column), limit, cancellationToken);

    [McpServerTool(Name = "find_implementations", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.FindImplementations)]
    public Task<IReadOnlyList<SymbolMatch>> FindImplementations(
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
        hierarchy.ImplementationsAsync(solutionId, new SymbolQuery(symbolId, name, file, line, column), limit, cancellationToken);

    [McpServerTool(Name = "find_callers", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.FindCallers)]
    public Task<IReadOnlyList<SymbolMatch>> FindCallers(
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
        hierarchy.CallersAsync(solutionId, new SymbolQuery(symbolId, name, file, line, column), limit, cancellationToken);

    [McpServerTool(Name = "get_type_hierarchy", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.GetTypeHierarchy)]
    public Task<TypeHierarchy?> GetTypeHierarchy(
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
        hierarchy.HierarchyAsync(solutionId, new SymbolQuery(symbolId, name, file, line, column), cancellationToken);

    [McpServerTool(Name = "get_generated_code", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.GetGeneratedCode)]
    public Task<GeneratedCodePage> GetGeneratedCode(
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
        generated.ReadAsync(solutionId, project, hintName, includeText, limit, cancellationToken);
}
