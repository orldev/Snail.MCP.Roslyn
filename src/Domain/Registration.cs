namespace Snail.MCP.Roslyn.Domain;

internal sealed record Registration(
    string Service,
    string? Implementation,
    string Lifetime,
    string Method,
    string File,
    int Line,
    string Snippet);
