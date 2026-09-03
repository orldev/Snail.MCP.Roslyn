using Microsoft.Extensions.Hosting;
using Snail.MCP.Roslyn.Extensions;

await Host.CreateApplicationBuilder(args)
    .ConfigureMcpServer()
    .Build()
    .RunAsync();