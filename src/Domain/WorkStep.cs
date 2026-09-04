namespace Snail.MCP.Roslyn.Domain;

/// <summary>One step of work a slow call has finished, for a caller that would otherwise see only silence.</summary>
/// <param name="Total">How many steps there are, when that is known before the work starts; null when it is not.</param>
public sealed record WorkStep(int Done, int? Total, string What);
