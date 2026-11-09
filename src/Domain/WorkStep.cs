namespace Snail.MCP.Roslyn.Domain;

/// <summary>One step of work a slow call has finished, for a caller that would otherwise see only silence.</summary>
/// <param name="Done">How many steps are finished.</param>
/// <param name="Total">How many steps there are, when that is known before the work starts; null when it is not.</param>
/// <param name="What">The step in words, as the caller is shown it.</param>
internal sealed record WorkStep(int Done, int? Total, string What);
