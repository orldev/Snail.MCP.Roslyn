using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Snail.MCP.Roslyn.Tools.Prompts;

namespace Snail.MCP.Roslyn.Tools;

[McpServerToolType]
public sealed class SolutionTools(ISolutionHost host, IDiagnosticReader diagnostics, ISolutionSurveyor surveyor, IFileReader reader)
{
    [McpServerTool(Name = "load_solution")]
    [Description(ToolDescriptions.LoadSolution)]
    public Task<WorkspaceReport> LoadSolution(
        [Description("Absolute path to a .sln, .slnx or .csproj file, or a directory containing one.")]
        string path,
        CancellationToken cancellationToken,
        IProgress<ProgressNotificationValue> progress,
        [Description("Also count compiler errors and warnings. Off by default: it costs a full build and the numbers go stale on the next edit.")]
        bool countDiagnostics = false) =>
        host.LoadAsync(path, cancellationToken, countDiagnostics, new ToolProgress(progress));

    [McpServerTool(Name = "get_diagnostics")]
    [Description(ToolDescriptions.GetDiagnostics)]
    public Task<IssuePage> GetDiagnostics(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Optional absolute file or directory path that narrows the results.")]
        string? scope = null,
        [Description("Lowest severity to include: Info, Warning or Error.")]
        IssueSeverity minimum = IssueSeverity.Warning,
        [Description("Maximum issues to return.")]
        int limit = 50,
        [Description("Include diagnostics that only exist in generated code the caller cannot edit.")]
        bool includeGenerated = false) =>
        diagnostics.ReadAsync(solutionId, scope, minimum, limit, includeGenerated, cancellationToken);

    [McpServerTool(Name = "read_file")]
    [Description(ToolDescriptions.ReadFile)]
    public Task<FileView> ReadFile(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        [Description("Absolute path of the file to read.")]
        string file,
        CancellationToken cancellationToken,
        [Description("One-based line to start from.")]
        int startLine = 1,
        [Description("Maximum lines of text to return.")]
        int maxLines = 2000) =>
        reader.ReadAsync(solutionId, file, startLine, maxLines, cancellationToken);

    [McpServerTool(Name = "get_solution_overview")]
    [Description(ToolDescriptions.GetSolutionOverview)]
    public Task<SolutionOverview> GetSolutionOverview(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken,
        IProgress<ProgressNotificationValue> progress,
        [Description("Maximum entries per section of the answer.")]
        int limit = 20) =>
        surveyor.OverviewAsync(solutionId, limit, cancellationToken, new ToolProgress(progress));

    [McpServerTool(Name = "check_architecture")]
    [Description(ToolDescriptions.CheckArchitecture)]
    public Task<ArchitectureReport> CheckArchitecture(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken,
        [Description("Maximum findings to return.")]
        int limit = 50) =>
        surveyor.CheckAsync(solutionId, limit, cancellationToken);

    [McpServerTool(Name = "health_check")]
    [Description(ToolDescriptions.HealthCheck)]
    public Task<HealthReport> HealthCheck(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken) =>
        surveyor.HealthAsync(solutionId, cancellationToken);

    [McpServerTool(Name = "unload_solution")]
    [Description(ToolDescriptions.UnloadSolution)]
    public Task<UnloadReport> UnloadSolution(
        [Description(ParameterDescriptions.Solution)]
        string solutionId,
        CancellationToken cancellationToken) =>
        host.UnloadAsync(solutionId, cancellationToken);
}
