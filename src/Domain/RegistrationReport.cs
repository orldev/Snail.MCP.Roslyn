namespace Snail.MCP.Roslyn.Domain;

/// <summary>Container registrations found in source, with the interfaces nobody registered.</summary>
public sealed record RegistrationReport(
    IReadOnlyList<Registration> Items,
    IReadOnlyList<SymbolMatch> Unregistered,
    int Total,
    bool Truncated,
    IReadOnlyList<string> Notes);
