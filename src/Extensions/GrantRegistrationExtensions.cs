using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Snail.MCP.Roslyn.Application;

namespace Snail.MCP.Roslyn.Extensions;

/// <summary>The grant of a session, checked where calls arrive and again by the one tool that forwards them.</summary>
public static class GrantRegistrationExtensions
{
    extension(IMcpServerBuilder builder)
    {
        /// <summary>Refuses a tool the session was not granted, whoever sent the call.</summary>
        /// <remarks>
        /// At the door, so that every call the protocol carries passes the same check, whichever tool it
        /// names and whoever sent it. The door sees only what arrives through the protocol: a tool that
        /// invokes another tool's object itself is past it, so <c>run_tools</c> checks the grant again for
        /// every step (<see cref="Tools.SequenceTools"/>), and a tool added later that forwards calls must do
        /// the same.
        /// </remarks>
        public IMcpServerBuilder RefuseWhatWasNotGranted()
        {
            builder.WithRequestFilters(filters => filters.AddCallToolFilter(Granted));

            return builder;
        }
    }

    private static McpRequestHandler<CallToolRequestParams, CallToolResult> Granted(McpRequestHandler<CallToolRequestParams, CallToolResult> next) =>
        async (context, cancellationToken) =>
        {
            var grant = context.Server.Services?.GetService<SessionGrant>();

            if (context.Params?.Name is not { } name || grant is null || grant.Allows(name))
                return await next(context, cancellationToken);

            return new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = grant.Refusal(name) }]
            };
        };
}
