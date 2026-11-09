namespace Snail.MCP.Roslyn.Domain;

/// <summary>Whether a loaded solution can answer semantic questions.</summary>
internal enum WorkspaceReadiness
{
    Ready,
    Restoring,
    Degraded,
    Failed
}
