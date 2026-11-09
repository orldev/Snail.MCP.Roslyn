namespace Snail.MCP.Roslyn.Application.Ports;

/// <summary>Reads dependency injection registrations, including the ones written by name and invisible to a rename.</summary>
internal interface IContainerReader
{
    Task<RegistrationReport> ReadAsync(string solutionId, string? service, int limit, CancellationToken cancellationToken);
}
