using BenchmarkDotNet.Attributes;
using NServiceBus.Attachments.FileShare;

[MemoryDiagnoser]
[GcServer(true)]
public class PersisterBenchmarks
{
    string directory = null!;
    Persister persister = null!;
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

    byte[] NewData()
    {
        var data = new byte[DataSize];
        Random.Shared.NextBytes(data);
        return data;
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Fresh subdirectory per iteration for isolation
        var iterDir = Path.Combine(directory, $"iter{Interlocked.Increment(ref counter)}");
        Directory.CreateDirectory(iterDir);
        persister = new(iterDir);
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
        var data = NewData();
        var stream = new MemoryStream(data);
        return persister.SaveStream(
            "msg1", "attachment", DateTime.UtcNow.AddDays(1), stream, null);
    }

    [Benchmark]
    public Task SaveBytes()
    {
        var data = NewData();
        return persister.SaveBytes(
            "msg1", "attachment", DateTime.UtcNow.AddDays(1), data, null);
    }

    [Benchmark]
    public async Task SaveAndGetBytes()
    {
        var data = NewData();
        await persister.SaveBytes(
            "msg1", "attachment", DateTime.UtcNow.AddDays(1), data, null);
        await persister.GetBytes("msg1", "attachment");
    }

    [Benchmark]
    public async Task SaveAndCopyTo()
    {
        var data = NewData();
        var stream = new MemoryStream(data);
        await persister.SaveStream(
            "msg1", "attachment", DateTime.UtcNow.AddDays(1), stream, null);
        await persister.CopyTo("msg1", "attachment", Stream.Null);
    }

    [Benchmark]
    public async Task SaveAndGetStream()
    {
        var data = NewData();
        var stream = new MemoryStream(data);
        await persister.SaveStream(
            "msg1", "attachment", DateTime.UtcNow.AddDays(1), stream, null);
        await using var result = await persister.GetStream("msg1", "attachment");
    }
}
