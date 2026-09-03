namespace Snail.MCP.Roslyn.Domain;

/// <summary>Addresses a symbol by identifier, by bare name, or by a position in source.</summary>
public sealed record SymbolQuery(string? Id, string? Name, string? File, int Line, int Column)
{
    public bool HasPosition => !string.IsNullOrWhiteSpace(File);

    public bool HasName => !string.IsNullOrWhiteSpace(Name);
}
