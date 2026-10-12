namespace Snail.MCP.Roslyn;

/// <summary>What the server says about the solutions it opens, the restores it runs, the sessions it refuses and the tools that fail.</summary>
/// <remarks>
/// Generated rather than written through the logging extensions: a line costs nothing while its level is off, where the
/// extensions boxed and formatted their arguments first. The messages are the ones the server has always written.
/// </remarks>
internal static partial class Log
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to open {Entry}")]
    internal static partial void OpenFailed(ILogger logger, Exception error, string entry);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Reused {Entry} as {Identity}")]
    internal static partial void Reused(ILogger logger, string entry, string identity);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Restored {Entry} in {Elapsed} ms")]
    internal static partial void Restored(ILogger logger, string entry, long elapsed);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Restore of {Entry} exited with {Code}: {Output}")]
    internal static partial void RestoreExited(ILogger logger, string entry, int code, string output);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Restore of {Entry} passed its {Seconds} s limit")]
    internal static partial void RestoreTimedOut(ILogger logger, string entry, int seconds);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "Restore of {Entry} could not be started")]
    internal static partial void RestoreNotStarted(ILogger logger, Exception error, string entry);

    [LoggerMessage(EventId = 7, Level = LogLevel.Warning, Message = "A session was turned away: {Reason}")]
    internal static partial void SessionTurnedAway(ILogger logger, string reason);

    [LoggerMessage(EventId = 8, Level = LogLevel.Error, Message = "Tool {Tool} failed")]
    internal static partial void ToolFailed(ILogger logger, Exception error, string? tool);
}
