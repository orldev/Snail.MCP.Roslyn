namespace Snail.MCP.Roslyn.Domain;

public sealed record RegistrationReport(
    IReadOnlyList<Registration> Items,
    IReadOnlyList<SymbolMatch> Unregistered,
    int Total,
    bool Truncated,
    IReadOnlyList<string> Notes);
