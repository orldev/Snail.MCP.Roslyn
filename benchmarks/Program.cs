using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Snail.MCP.Roslyn.Benchmarks.UnhandledBenchmarks).Assembly).Run(args);
