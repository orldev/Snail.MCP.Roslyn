using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Options;
using Snail.MCP.Roslyn.Adapters.Paths;
using Snail.MCP.Roslyn.Adapters.Workspaces;
using Snail.MCP.Roslyn.Configuration;

namespace Snail.MCP.Roslyn.Benchmarks;

/// <summary>
/// What every rename plan does after the compiler's references: a pass over the text of every document for the
/// occurrences it does not treat as references — comments, strings, unrelated names.
/// </summary>
[MemoryDiagnoser]
[ShortRunJob]
public class UnhandledBenchmarks
{
    private const string Name = "OrderLedger";

    private Solution _solution = null!;

    private TextScanner _scanner = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("Bench", LanguageNames.CSharp);
        var solution = project.Solution;

        for (var index = 0; index < 300; index++)
        {
            var source = index % 10 == 0 ? Mentioning(index) : Plain(index);
            var id = DocumentId.CreateNewId(project.Id);

            solution = solution.AddDocument(id, $"File{index}.cs", SourceText.From(source), filePath: $"/bench/src/File{index}.cs");
        }

        _solution = solution;
        _scanner = new TextScanner(new RootPathMap(Options.Create(new WorkspaceOptions())));

        await Scan();
    }

    [Benchmark]
    public async Task<int> Scan() => (await _scanner.UnhandledAsync(_solution, Name, [], CancellationToken.None)).Count;

    private static string Plain(int index) => $$"""
        namespace Bench;

        /// <summary>Type number {{index}}.</summary>
        public sealed class Type{{index}}
        {
            private readonly int _value = {{index}};

            public int Twice() => _value * 2;

            public string Describe() => $"type {_value}";
        }
        """;

    private static string Mentioning(int index) => $$"""
        namespace Bench;

        // Reads the OrderLedger before anything else.
        public sealed class Type{{index}}
        {
            private const string Label = "OrderLedger";

            public string Describe() => Label + " {{index}}";

            public int OrderLedgerCount { get; } = {{index}};
        }
        """;
}
