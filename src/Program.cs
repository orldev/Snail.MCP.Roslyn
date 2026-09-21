using Microsoft.Extensions.Hosting;
using Snail.MCP.Roslyn.Extensions;

await McpServerHost.Of(args).RunAsync();
