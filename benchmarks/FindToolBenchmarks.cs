using BenchmarkDotNet.Attributes;
using Snail.MCP.Roslyn.Tools;

namespace Snail.MCP.Roslyn.Benchmarks;

/// <summary>What find_tool does on every call: score each tool against the task and build the answer.</summary>
[MemoryDiagnoser]
[ShortRunJob]
public class FindToolBenchmarks
{
    private readonly ToolIndex _index = new();

    [Benchmark]
    public string Matched() => _index.Answer("who calls this method and where is it used", 5).ToJsonString();

    [Benchmark]
    public string Unmatched() => _index.Answer("zebra quartz", 5).ToJsonString();
}
