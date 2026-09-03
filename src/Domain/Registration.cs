namespace Snail.MCP.Roslyn.Domain;

/// <summary>One service registered with a dependency injection container.</summary>
public sealed record Registration(
    string Service,
    string? Implementation,
    string Lifetime,
    string Method,
    string File,
    int Line,
    string Snippet);
