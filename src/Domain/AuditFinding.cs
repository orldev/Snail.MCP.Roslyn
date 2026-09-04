namespace Snail.MCP.Roslyn.Domain;

public sealed record AuditFinding(SymbolMatch Symbol, string Reason);
