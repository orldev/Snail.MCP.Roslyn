using System.Text.Json.Serialization.Metadata;
using ModelContextProtocol.Protocol;
using Snail.MCP.Roslyn.Adapters.Workspaces;
using Snail.Sdk.Mcp.Hosting.Answers;

namespace Snail.MCP.Roslyn.Tools;

/// <summary>A tool's work answered in the envelope every server of the fleet answers in: <c>{"ok":true,"data":…}</c>.</summary>
/// <remarks>
/// The text alone (<see cref="AnswerShape.None"/>): no tool states an output schema, and structured content beside the
/// text carries the same data a second time, which on a page of references or a whole file doubles what the caller is
/// sent — and a client that reads the structured content first would see the data without the envelope. A refusal found
/// deep inside is thrown as <see cref="ToolRefusalException"/> and answered by <c>ExplainFailures</c>.
/// </remarks>
internal static class Answering
{
    extension<T>(Task<T> work)
    {
        /// <summary>The work's record as the data of a successful answer.</summary>
        public async Task<CallToolResult> AnsweredAsync(JsonTypeInfo<T> type) =>
            ToolAnswer.Ok(await work, type, AnswerShape.None);
    }

    extension<T>(Task<T?> work)
        where T : class
    {
        /// <summary>The work's record as the data of a successful answer, or the refusal <paramref name="error"/> when it found nothing.</summary>
        public async Task<CallToolResult> FoundAsync(JsonTypeInfo<T> type, string error, string hint) =>
            await work is { } found
                ? ToolAnswer.Ok(found, type, AnswerShape.None)
                : ToolAnswer.Failed(error, hint, RefusalTypes.NotFound);
    }
}
