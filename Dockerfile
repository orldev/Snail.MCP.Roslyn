# The server as a container: one per machine, standing behind every agent on it.
#
#   docker build -t snail/s3l-roslyn-mcp .
#   docker run -p 8821:8821 -e SNAIL_MCP_ROSLYN_PORT=8821 -v workspaces:/workspaces -v nuget:/nuget snail/s3l-roslyn-mcp
#
# Two things this image cannot do without.
#
# The SDK, not the runtime: MSBuildLocator loads MSBuild from the installed SDK, and a solution opened
# without one answers Degraded with semantics missing. That is why the runtime stage is the SDK image. Both stages
# are pinned to the band global.json asks for: on the floating sdk:10.0 a newer feature band would one day refuse
# the build, and the solutions this server opens pin the same band. The server is framework-dependent
# (-p:PublishAot=false), as Roslyn and MSBuild load code while it runs.
#
# The volume of working copies, because this server answers about the task's code and must run where
# the code is. Each session names the copy it works in through its address — …/mcp?root=/workspaces/<task>
# — and a session naming a copy this container cannot see is refused at the door. The package cache is
# mounted for the same reason it is mounted into a worker: a warm restore cannot be baked into an image,
# and without one every solution loads Degraded.
#
# VERSION is the release's when the shared mcp-image.yml builds the image (0.0.0-<branch>.<commit> from a branch)
# and 0.0.0-local otherwise: the server reports it in initialize and the image carries it as its OCI version.
ARG VERSION=0.0.0-local

FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
ARG VERSION
WORKDIR /source
COPY . .
RUN dotnet publish src/Snail.MCP.Roslyn.csproj -c Release -o /app -p:PublishAot=false -p:Version=$VERSION

FROM mcr.microsoft.com/dotnet/sdk:10.0.302
ARG VERSION
LABEL org.opencontainers.image.version=$VERSION
# tini runs as process 1 and starts the program under it: .NET ends an unhandled exception with SIGABRT, which process
# 1 of a container ignores, so run as process 1 itself a program that fails at start spins at a full core and never
# exits, and nothing restarts it. Under tini it exits with 134.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends tini \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .

ENV SNAIL_MCP_ROSLYN_PORT=8821 \
    SNAIL_MCP_ROSLYN_SESSION_ROOTS__0=/workspaces \
    NUGET_PACKAGES=/nuget \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
EXPOSE 8821

ENTRYPOINT ["/usr/bin/tini", "--", "dotnet", "Snail.MCP.Roslyn.dll"]
