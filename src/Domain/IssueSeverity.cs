namespace Snail.MCP.Roslyn.Domain;

/// <summary>How much a diagnostic matters, independent of the compiler that produced it.</summary>
internal enum IssueSeverity
{
    Info,
    Warning,
    Error
}
