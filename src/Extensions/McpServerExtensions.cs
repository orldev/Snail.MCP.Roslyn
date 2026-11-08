using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Roslyn.Configuration;
using Snail.Sdk.Mcp.Hosting.Answers;

namespace Snail.MCP.Roslyn.Extensions;

public static class McpServerExtensions
{
    /// <summary>The type of the refusal of an answer past the budget.</summary>
    public const string TooLarge = "tooLarge";

    extension(IMcpServerBuilder builder)
    {
        /// <summary>Refuses an answer too large to reach the caller whole, instead of letting it be cut.</summary>
        /// <remarks>Every tool caps how many items it returns, and a count is not a size: run_analyzers at its
        /// limit of 200 already serialises to some 90 000 characters against this repository alone. A client
        /// that cuts an answer to fit leaves JSON that stops mid-structure, and a plan read out of a cut answer
        /// is a plan applied in part. Refusing names the size and what to lower, which the caller can act on,
        /// in the failure envelope every other refusal of the server answers in.
        /// <para>
        /// The content is measured, not the structured content beside it: an answer of Snail.Sdk's tools —
        /// <c>find_tool</c>, <c>run_tools</c> — carries its data in both, and counted twice, the whole catalogue
        /// <c>find_tool</c> answers when nothing matches came to 70 000 characters and was refused.
        /// </para></remarks>
        public IMcpServerBuilder BoundAnswers()
        {
            builder.WithRequestFilters(filters => filters.AddCallToolFilter(Bounded));

            return builder;
        }
    }

    private static McpRequestHandler<CallToolRequestParams, CallToolResult> Bounded(McpRequestHandler<CallToolRequestParams, CallToolResult> next) =>
        async (context, cancellationToken) =>
        {
            var result = await next(context, cancellationToken);

            if (result.IsError == true)
                return result;

            var budget = context.Server.Services?.GetService<IOptions<WorkspaceOptions>>()
                ?.Value.MaxResponseChars ?? new WorkspaceOptions().MaxResponseChars;

            var size = JsonSerializer.Serialize(result.Content, McpJsonUtilities.DefaultOptions).Length;

            if (size <= budget)
                return result;

            return ToolAnswer.Failed(
                $"{context.Params?.Name}: the answer is {size} characters against a budget of {budget}, so it is not returned rather than returned cut in half",
                "Ask for less: a lower 'limit', a narrower 'scope' or 'file', fewer lines, or one project at a time.",
                TooLarge);
        };
}
