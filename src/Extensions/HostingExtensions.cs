using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Snail.MCP.Roslyn.Extensions;

public static class HostingExtensions
{
    extension(HostApplicationBuilder builder)
    {
        public HostApplicationBuilder ConfigureMcpServer()
        {
            builder.UseInstalledMsBuild();
            builder.Logging.UseStderrLogging();
            builder.Services.AddCodeAnalysis(builder.Configuration);
            builder.Services.AddMcpTransport();

            return builder;
        }

        /// <summary>Points the process at the MSBuild of the installed SDK, before any of it is loaded.</summary>
        public HostApplicationBuilder UseInstalledMsBuild()
        {
            if (!MSBuildLocator.IsRegistered)
                MSBuildLocator.RegisterDefaults();

            return builder;
        }
    }
}
