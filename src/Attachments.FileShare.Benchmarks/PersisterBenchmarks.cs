[MemoryDiagnoser]
[GcServer(true)]
[SimpleJob(warmupCount: 2, iterationCount: 5)]
public class PersisterBenchmarks
{
    string directory = null!;
    Persister persister = null!;
    byte[] data = null!;
    string stringData = null!;
    int counter;

    [Params(1024, 1024 * 100, 1024 * 1024, 1024 * 1024 * 10)]
    public int DataSize { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        directory = Path.Combine(Path.GetTempPath(), "AttachmentsBenchmark");
        Directory.CreateDirectory(directory);
        persister = new(directory);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Fresh subdirectory per iteration for isolation
        var iterDir = Path.Combine(directory, $"iter{Interlocked.Increment(ref counter)}");
        Directory.CreateDirectory(iterDir);
        persister = new(iterDir);
        data = new byte[DataSize];
        Random.Shared.NextBytes(data);
        stringData = new('x', DataSize);
    }

    [GlobalCleanup]
    public static void GlobalCleanup()
    {
        var dir = Path.Combine(Path.GetTempPath(), "AttachmentsBenchmark");
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }
    }

    [Benchmark]
    public Task SaveStream()
    {
        var stream = new MemoryStream(data);
        return persister.SaveStream(
            "msg1", "attachment", DateTime.UtcNow.AddDays(1), stream);
    }

    [Benchmark]
    public Task SaveBytes() =>
        persister.SaveStream(
            "msg1", "attachment", DateTime.UtcNow.AddDays(1), new MemoryStream(data));

    [Benchmark]
    public async Task SaveAndGetBytes()
    {
        await persister.SaveStream(
            "msg1", "attachment", DateTime.UtcNow.AddDays(1), new MemoryStream(data));
        await persister.GetBytes("msg1", "attachment");
    }

    [Benchmark]
    public Task SaveString() =>
        persister.SaveString(
            "msg1", "attachment", DateTime.UtcNow.AddDays(1), stringData);

    [Benchmark]
    public async Task SaveAndGetString()
    {
        await persister.SaveString(
            "msg1", "attachment", DateTime.UtcNow.AddDays(1), stringData);
        await persister.GetString("msg1", "attachment", null);
    }

    [Benchmark]
    public async Task SaveAndCopyTo()
    {
        var stream = new MemoryStream(data);
        await persister.SaveStream(
            "msg1", "attachment", DateTime.UtcNow.AddDays(1), stream);
        await persister.CopyTo("msg1", "attachment", Stream.Null);
    }

    [Benchmark]
    public async Task SaveAndGetStream()
    {
        var stream = new MemoryStream(data);
        await persister.SaveStream(
            "msg1", "attachment", DateTime.UtcNow.AddDays(1), stream);
        await using var result = await persister.GetStream("msg1", "attachment");
    }
}
