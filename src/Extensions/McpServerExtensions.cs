using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Snail.MCP.Roslyn.Configuration;

namespace Snail.MCP.Roslyn.Extensions;

public static class McpServerExtensions
{
    extension(IMcpServerBuilder builder)
    {
        /// <summary>Refuses an answer too large to reach the caller whole, instead of letting it be cut.</summary>
        /// <remarks>Every tool caps how many items it returns, and a count is not a size: run_analyzers at its
        /// limit of 200 already serialises to some 90 000 characters against this repository alone. A client
        /// that cuts an answer to fit leaves JSON that stops mid-structure, and a plan read out of a cut answer
        /// is a plan applied in part. Refusing names the size and what to lower, which the caller can act on.</remarks>
        public IMcpServerBuilder BoundAnswers()
        {
            builder.WithRequestFilters(filters => filters.AddCallToolFilter(next => async (context, cancellationToken) =>
            {
                var result = await next(context, cancellationToken);

                if (result.IsError == true)
                    return result;

                var budget = context.Server.Services?.GetService<IOptions<WorkspaceOptions>>()
                    ?.Value.MaxResponseChars ?? new WorkspaceOptions().MaxResponseChars;

                var size = JsonSerializer.Serialize(result, McpJsonUtilities.DefaultOptions).Length;

                if (size <= budget)
                    return result;

                return new CallToolResult
                {
                    IsError = true,
                    Content =
                    [
                        new TextContentBlock
                        {
                            Text = $"{context.Params?.Name}: the answer is {size} characters against a budget of "
                                + $"{budget}, so it is not returned rather than returned cut in half. Ask for less: "
                                + "a lower 'limit', a narrower 'scope' or 'file', fewer lines, or one project at a time."
                        }
                    ]
                };
            }));

            return builder;
        }

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
                    // The caller is handed the message alone; the trace behind it exists nowhere else.
                    context.Server.Services?.GetService<ILoggerFactory>()
                        ?.CreateLogger("Snail.MCP.Roslyn.Tools")
                        .LogError(error, "Tool {Tool} failed", context.Params?.Name);

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
