namespace Snail.MCP.Roslyn.Domain;

/// <summary>One symbol an audit flagged, with the checkable reason it was flagged.</summary>
public sealed record AuditFinding(SymbolMatch Symbol, string Reason);
