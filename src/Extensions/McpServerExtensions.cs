using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace Snail.MCP.Roslyn.Extensions;

public static class McpServerExtensions
{
    extension(IMcpServerBuilder builder)
    {
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
