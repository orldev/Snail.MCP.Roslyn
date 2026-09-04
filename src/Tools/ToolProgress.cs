using ModelContextProtocol;

namespace Snail.MCP.Roslyn.Tools;

/// <summary>Carries a step of work out to the caller as a protocol notification.</summary>
/// <remarks>The protocol type stops here: a port reports a WorkStep and knows nothing about MCP. The SDK hands
/// a tool a progress sink whether or not the client asked for notifications, so nothing here decides that.</remarks>
internal sealed class ToolProgress(IProgress<ProgressNotificationValue> notifications) : IProgress<WorkStep>
{
    public void Report(WorkStep step) =>
        notifications.Report(new ProgressNotificationValue
        {
            Progress = step.Done,
            Total = step.Total,
            Message = step.What
        });
}
