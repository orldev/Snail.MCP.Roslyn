namespace Snail.MCP.Roslyn.Domain;

internal sealed record CodeFixPage(
    string DiagnosticId,
    string Message,
    IReadOnlyList<CodeFix> Fixes,
    IReadOnlyList<string> Notes);
