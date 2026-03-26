var config = DefaultConfig.Instance
    .WithArtifactsPath("BenchmarkDotNet.Artifacts.FileShare");
BenchmarkSwitcher.FromAssembly(typeof(PersisterBenchmarks).Assembly).Run(args, config);
