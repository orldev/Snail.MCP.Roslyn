using System.ComponentModel;
using ModelContextProtocol.Server;
using Snail.MCP.Roslyn.Tools.Prompts;

namespace Snail.MCP.Roslyn.Tools;

/// <summary>Resolving names to symbols and following what uses them.</summary>
[McpServerToolType]
public sealed class SymbolTools(
    ISymbolLookup symbols,
    IReferenceFinder references,
    IHierarchyExplorer hierarchy,
    IGeneratedCodeReader generated)
{
    [McpServerTool(Name = "search_symbols")]
    [Description(ToolDescriptions.SearchSymbols)]
    public Task<IReadOnlyList<SymbolMatch>> SearchSymbols(
        [Description("Solution id returned by load_solution.")]
        string solutionId,
        [Description("Part of the symbol name; matching ignores case.")]
        string query,
        CancellationToken cancellationToken,
        [Description("Narrow to one declaration family.")]
        SymbolKindFilter kind = SymbolKindFilter.Any,
        [Description("Maximum candidates to return.")]
        int limit = 30) =>
        symbols.SearchAsync(solutionId, query, kind, limit, cancellationToken);

    [McpServerTool(Name = "get_symbol_info")]
    [Description(ToolDescriptions.GetSymbolInfo)]
    public Task<SymbolDetails?> GetSymbolInfo(
        [Description("Solution id returned by load_solution.")]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Symbol id from search_symbols. Give this or a file position.")]
        string? symbolId = null,
        [Description("Absolute file path, when addressing by position.")]
        string? file = null,
        [Description("One-based line, when addressing by position.")]
        int line = 0,
        [Description("One-based column, when addressing by position.")]
        int column = 0) =>
        symbols.DescribeAsync(solutionId, new SymbolQuery(symbolId, null, file, line, column), cancellationToken);

    [McpServerTool(Name = "get_symbol_source")]
    [Description(ToolDescriptions.GetSymbolSource)]
    public Task<SymbolSource?> GetSymbolSource(
        [Description("Solution id returned by load_solution.")]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Symbol id from search_symbols. Give this or a file position.")]
        string? symbolId = null,
        [Description("Absolute file path, when addressing by position.")]
        string? file = null,
        [Description("One-based line, when addressing by position.")]
        int line = 0,
        [Description("One-based column, when addressing by position.")]
        int column = 0) =>
        symbols.ReadAsync(solutionId, new SymbolQuery(symbolId, null, file, line, column), cancellationToken);

    [McpServerTool(Name = "find_references")]
    [Description(ToolDescriptions.FindReferences)]
    public Task<ReferencePage> FindReferences(
        [Description("Solution id returned by load_solution.")]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Symbol id from search_symbols. Give this or a file position.")]
        string? symbolId = null,
        [Description("Absolute file path, when addressing by position.")]
        string? file = null,
        [Description("One-based line, when addressing by position.")]
        int line = 0,
        [Description("One-based column, when addressing by position.")]
        int column = 0,
        [Description("Maximum references to return.")]
        int limit = 100) =>
        references.FindAsync(solutionId, new SymbolQuery(symbolId, null, file, line, column), limit, cancellationToken);

    [McpServerTool(Name = "find_implementations")]
    [Description(ToolDescriptions.FindImplementations)]
    public Task<IReadOnlyList<SymbolMatch>> FindImplementations(
        [Description("Solution id returned by load_solution.")]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Symbol id from search_symbols. Give this, a bare name, or a file position.")]
        string? symbolId = null,
        [Description("Bare symbol name; used only when it matches exactly one symbol.")]
        string? name = null,
        [Description("Absolute file path, when addressing by position.")]
        string? file = null,
        [Description("One-based line, when addressing by position.")]
        int line = 0,
        [Description("One-based column, when addressing by position.")]
        int column = 0,
        [Description("Maximum results to return.")]
        int limit = 50) =>
        hierarchy.ImplementationsAsync(solutionId, new SymbolQuery(symbolId, name, file, line, column), limit, cancellationToken);

    [McpServerTool(Name = "find_callers")]
    [Description(ToolDescriptions.FindCallers)]
    public Task<IReadOnlyList<SymbolMatch>> FindCallers(
        [Description("Solution id returned by load_solution.")]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Symbol id from search_symbols. Give this, a bare name, or a file position.")]
        string? symbolId = null,
        [Description("Bare symbol name; used only when it matches exactly one symbol.")]
        string? name = null,
        [Description("Absolute file path, when addressing by position.")]
        string? file = null,
        [Description("One-based line, when addressing by position.")]
        int line = 0,
        [Description("One-based column, when addressing by position.")]
        int column = 0,
        [Description("Maximum results to return.")]
        int limit = 50) =>
        hierarchy.CallersAsync(solutionId, new SymbolQuery(symbolId, name, file, line, column), limit, cancellationToken);

    [McpServerTool(Name = "get_type_hierarchy")]
    [Description(ToolDescriptions.GetTypeHierarchy)]
    public Task<TypeHierarchy?> GetTypeHierarchy(
        [Description("Solution id returned by load_solution.")]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Symbol id from search_symbols. Give this, a bare name, or a file position.")]
        string? symbolId = null,
        [Description("Bare type name; used only when it matches exactly one type.")]
        string? name = null,
        [Description("Absolute file path, when addressing by position.")]
        string? file = null,
        [Description("One-based line, when addressing by position.")]
        int line = 0,
        [Description("One-based column, when addressing by position.")]
        int column = 0) =>
        hierarchy.HierarchyAsync(solutionId, new SymbolQuery(symbolId, name, file, line, column), cancellationToken);

    [McpServerTool(Name = "get_generated_code")]
    [Description(ToolDescriptions.GetGeneratedCode)]
    public Task<GeneratedCodePage> GetGeneratedCode(
        [Description("Solution id returned by load_solution.")]
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
