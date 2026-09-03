namespace Snail.MCP.Roslyn.Extensions;

/// <summary>Keeps standard output owned by the protocol.</summary>
public static class LoggingExtensions
{
    extension(ILoggingBuilder logging)
    {
        public ILoggingBuilder UseStderrLogging()
        {
            logging.ClearProviders();
            logging.AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace);

            return logging;
        }
    }
}
