namespace Snail.MCP.Roslyn.Domain;

/// <summary>Whether a loaded solution can answer semantic questions.</summary>
public enum WorkspaceReadiness
{
    Ready,
    Restoring,
    Degraded,
    Failed
}
