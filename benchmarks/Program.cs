using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Snail.MCP.Roslyn.Benchmarks.FindToolBenchmarks).Assembly).Run(args);
