using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(PersisterBenchmarks).Assembly).Run(args);
