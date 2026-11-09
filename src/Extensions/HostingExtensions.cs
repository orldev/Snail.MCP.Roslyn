using Microsoft.Build.Locator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Snail.MCP.Roslyn.Extensions;

/// <summary>Composition root: the two shapes this server runs in, and what they share.</summary>
/// <remarks>
/// At an address every session names the solution it works in, so one server stands behind every worker
/// of a machine and its warm compilations are shared instead of rebuilt per step. As a program it belongs
/// to the one caller that started it and reaches wherever that caller does.
/// </remarks>
internal static class HostingExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        /// <summary>Configures the server to answer sessions at an address, each naming the solution it works in.</summary>
        public IHostApplicationBuilder ConfigureMcpServerAtAnAddress(IConfiguration settings)
        {
            builder.UseInstalledMsBuild();
            builder.Services.AddCodeAnalysis(settings);
            builder.Services.AddMcpTransportAtAnAddress(settings);

            return builder;
        }

        /// <summary>Configures the server to speak stdio to the process that started it.</summary>
        public IHostApplicationBuilder ConfigureMcpServerAsAProgram(IConfiguration settings)
        {
            builder.UseInstalledMsBuild();
            builder.Services.AddCodeAnalysis(settings);
            builder.Services.AddMcpTransportAsAProgram(settings);

            return builder;
        }

        /// <summary>Points the process at the MSBuild of the installed SDK, before any of it is loaded.</summary>
        public IHostApplicationBuilder UseInstalledMsBuild()
        {
            if (!MSBuildLocator.IsRegistered)
                MSBuildLocator.RegisterDefaults();

            return builder;
        }
    }
}
