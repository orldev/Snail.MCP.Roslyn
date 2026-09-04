using System.ComponentModel;
using ModelContextProtocol.Server;
using Snail.MCP.Roslyn.Tools.Prompts;

namespace Snail.MCP.Roslyn.Tools;

[McpServerToolType]
public sealed class ArchitectureTools(IContainerReader container, ICodeUsageAuditor auditor)
{
    [McpServerTool(Name = "get_di_registrations")]
    [Description(ToolDescriptions.GetDiRegistrations)]
    public Task<RegistrationReport> GetDiRegistrations(
        [Description("Solution id returned by load_solution.")]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Part of a service type name, to narrow the list.")]
        string? service = null,
        [Description("Maximum registrations to return.")]
        int limit = 100) =>
        container.ReadAsync(solutionId, service, limit, cancellationToken);

    [McpServerTool(Name = "find_unused_symbols")]
    [Description(ToolDescriptions.FindUnusedSymbols)]
    public Task<AuditPage> FindUnusedSymbols(
        [Description("Solution id returned by load_solution.")]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Optional absolute file or directory path that narrows the audit.")]
        string? scope = null,
        [Description("Maximum findings to return.")]
        int limit = 50) =>
        auditor.UnusedAsync(solutionId, scope, limit, cancellationToken);

    [McpServerTool(Name = "find_uncovered_symbols")]
    [Description(ToolDescriptions.FindUncoveredSymbols)]
    public Task<AuditPage> FindUncoveredSymbols(
        [Description("Solution id returned by load_solution.")]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Optional absolute file or directory path that narrows the audit.")]
        string? scope = null,
        [Description("Maximum findings to return.")]
        int limit = 50) =>
        auditor.UncoveredAsync(solutionId, scope, limit, cancellationToken);

    [McpServerTool(Name = "find_tests_for_symbol")]
    [Description(ToolDescriptions.FindTestsForSymbol)]
    public Task<IReadOnlyList<SymbolMatch>> FindTestsForSymbol(
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
        [Description("Maximum test methods to return.")]
        int limit = 50) =>
        auditor.TestsForAsync(solutionId, new SymbolQuery(symbolId, name, file, line, column), limit, cancellationToken);
}
