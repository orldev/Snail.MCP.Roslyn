using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Roslyn.Tools.Prompts;

namespace Snail.MCP.Roslyn.Tools;

[McpServerToolType]
internal sealed class ArchitectureTools(IContainerReader container, ICodeUsageAuditor auditor)
{
    [McpServerTool(Name = "get_di_registrations", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.GetDiRegistrations)]
    public Task<CallToolResult> GetDiRegistrations(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Part of a service type name, to narrow the list.")]
        string? service = null,
        [Description("Maximum registrations to return.")]
        int limit = 100) =>
        container.ReadAsync(solutionId, service, limit, cancellationToken)
            .AnsweredAsync(RoslynJson.Default.RegistrationReport);

    [McpServerTool(Name = "find_unused_symbols", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.FindUnusedSymbols)]
    public Task<CallToolResult> FindUnusedSymbols(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Optional absolute file or directory path that narrows the audit.")]
        string? scope = null,
        [Description("Maximum findings to return.")]
        int limit = 50) =>
        auditor.UnusedAsync(solutionId, scope, limit, cancellationToken)
            .AnsweredAsync(RoslynJson.Default.AuditPage);

    [McpServerTool(Name = "find_uncovered_symbols", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.FindUncoveredSymbols)]
    public Task<CallToolResult> FindUncoveredSymbols(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Optional absolute file or directory path that narrows the audit.")]
        string? scope = null,
        [Description("Maximum findings to return.")]
        int limit = 50) =>
        auditor.UncoveredAsync(solutionId, scope, limit, cancellationToken)
            .AnsweredAsync(RoslynJson.Default.AuditPage);

    [McpServerTool(Name = "find_tests_for_symbol", ReadOnly = true, OpenWorld = false)]
    [Description(ToolDescriptions.FindTestsForSymbol)]
    public Task<CallToolResult> FindTestsForSymbol(
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
        [Description("Maximum test methods to return.")]
        int limit = 50) =>
        auditor.TestsForAsync(solutionId, new SymbolQuery(symbolId, name, file, line, column), limit, cancellationToken)
            .AnsweredAsync(RoslynJson.Default.IReadOnlyListSymbolMatch);
}
