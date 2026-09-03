namespace Snail.MCP.Roslyn.Domain;

/// <summary>Narrows a symbol search to one declaration family.</summary>
public enum SymbolKindFilter
{
    Any,
    Type,
    Method,
    Property,
    Field,
    Event
}
