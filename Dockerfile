# The server as a container: one per machine, standing behind every agent on it.
#
#   docker build -t snail/mcp-roslyn .
#   docker run -p 8821:8821 -e Workspace__Port=8821 -v workspaces:/workspaces -v nuget:/nuget snail/mcp-roslyn
#
# Two things this image cannot do without.
#
# The SDK, not the runtime: MSBuildLocator loads MSBuild from the installed SDK, and a solution opened
# without one answers Degraded with semantics missing. That is why the runtime stage is the SDK image.
#
# The volume of working copies, because this server answers about the task's code and must run where
# the code is. Each session names the copy it works in through its address — …/mcp?root=/workspaces/<task>
# — and a session naming a copy this container cannot see is refused at the door. The package cache is
# mounted for the same reason it is mounted into a worker: a warm restore cannot be baked into an image,
# and without one every solution loads Degraded.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet publish src/Snail.MCP.Roslyn.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app
COPY --from=build /app .

ENV Workspace__Port=8821 \
    NUGET_PACKAGES=/nuget \
    DOTNET_CLI_TELEMETRY_OPTOUT=1
EXPOSE 8821

ENTRYPOINT ["dotnet", "Snail.MCP.Roslyn.dll"]
