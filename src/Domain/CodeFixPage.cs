namespace Snail.MCP.Roslyn.Domain;

public sealed record CodeFixPage(
    string DiagnosticId,
    string Message,
    IReadOnlyList<CodeFix> Fixes,
    IReadOnlyList<string> Notes);
