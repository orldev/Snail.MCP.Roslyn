namespace Snail.MCP.Roslyn.Domain;

/// <summary>Repairs the compiler and its analyzers offer for one diagnostic.</summary>
public sealed record CodeFixPage(
    string DiagnosticId,
    string Message,
    IReadOnlyList<CodeFix> Fixes,
    IReadOnlyList<string> Notes);
