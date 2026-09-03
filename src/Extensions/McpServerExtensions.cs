using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace Snail.MCP.Roslyn.Extensions;

/// <summary>Server-side behaviour that belongs to the protocol rather than to any one tool.</summary>
public static class McpServerExtensions
{
    extension(IMcpServerBuilder builder)
    {
        /// <summary>Turns a thrown exception into an answer that says what went wrong, instead of an opaque failure.</summary>
        public IMcpServerBuilder ExplainFailures()
        {
            builder.WithRequestFilters(filters => filters.AddCallToolFilter(next => async (context, cancellationToken) =>
            {
                try
                {
                    return await next(context, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception error)
                {
                    return new CallToolResult
                    {
                        IsError = true,
                        Content = [new TextContentBlock { Text = $"{context.Params?.Name}: {error.Message}" }]
                    };
                }
            }));

            return builder;
        }
    }
}
