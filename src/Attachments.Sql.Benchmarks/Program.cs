var config = DefaultConfig.Instance
    .WithArtifactsPath("BenchmarkDotNet.Artifacts.Sql");
BenchmarkSwitcher.FromAssembly(typeof(PersisterBenchmarks).Assembly).Run(args, config);
