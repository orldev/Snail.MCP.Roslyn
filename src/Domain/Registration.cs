namespace Snail.MCP.Roslyn.Domain;

public sealed record Registration(
    string Service,
    string? Implementation,
    string Lifetime,
    string Method,
    string File,
    int Line,
    string Snippet);
