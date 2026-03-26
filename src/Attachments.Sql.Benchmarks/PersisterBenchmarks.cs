[MemoryDiagnoser]
[GcServer(true)]
public class PersisterBenchmarks
{
    static SqlInstance sqlInstance = null!;
    SqlDatabase database = null!;
    SqlConnection connection = null!;
    Persister persister = null!;
    byte[] data = null!;
    int counter;

    [Params(1024, 1024 * 100, 1024 * 1024, 1024 * 1024 * 10)]
    public int DataSize { get; set; }

    [GlobalSetup]
    #pragma warning disable CA1822
    public void GlobalSetup() =>
        sqlInstance = new(
            "AttachmentsBenchmark",
            async connection =>
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    create table [dbo].[MessageAttachments](
                        Id uniqueidentifier default newsequentialid() primary key not null,
                        MessageId nvarchar(50) not null,
                        MessageIdLower as lower(MessageId),
                        Name nvarchar(255) not null,
                        NameLower as lower(Name),
                        Created datetime2(0) not null default sysutcdatetime(),
                        Expiry datetime2(0) not null,
                        Metadata nvarchar(max),
                        Data varbinary(max) not null
                    );
                    create unique index Index_MessageIdName
                        on [dbo].[MessageAttachments](MessageIdLower, NameLower);
                    """;
                await command.ExecuteNonQueryAsync();
            });
    #pragma warning restore CA1822

    [IterationSetup]
    public void IterationSetup()
    {
        database = sqlInstance.Build($"Bench{Interlocked.Increment(ref counter)}").GetAwaiter().GetResult();
        connection = database.Connection;
        var dbName = new SqlConnectionStringBuilder(database.ConnectionString).InitialCatalog;
        persister = new(dbName);
        data = new byte[DataSize];
        Random.Shared.NextBytes(data);
    }

    [IterationCleanup]
    public void IterationCleanup() =>
        database.Dispose();

    [GlobalCleanup]
    public static void GlobalCleanup()
    {
        sqlInstance.Cleanup();
        sqlInstance.Dispose();
    }

    [Benchmark]
    public async Task SaveStream()
    {
        var dataSize = DataSize;
        var pipe = new Pipe();
        var writerTask = Task.Run(async () =>
        {
            try
            {
                const int chunkSize = 8192;
                var remaining = dataSize;
                while (remaining > 0)
                {
                    var size = Math.Min(chunkSize, remaining);
                    var buffer = pipe.Writer.GetMemory(size);
                    Random.Shared.NextBytes(buffer.Span[..size]);
                    pipe.Writer.Advance(size);
                    await pipe.Writer.FlushAsync();
                    remaining -= size;
                }
            }
            finally
            {
                await pipe.Writer.CompleteAsync();
            }
        });
        var readerStream = pipe.Reader.AsStream();
        await using (readerStream)
        {
            await persister.SaveStream(
                connection, null, "msg1", "attachment", DateTime.UtcNow.AddDays(1), readerStream);
            await writerTask;
        }
    }

    [Benchmark]
    public Task<Guid> SaveBytes() =>
        persister.SaveBytes(
            connection, null, "msg1", "attachment", DateTime.UtcNow.AddDays(1), data);

    [Benchmark]
    public async Task SaveAndGetBytes()
    {
        await persister.SaveBytes(
            connection, null, "msg1", "attachment", DateTime.UtcNow.AddDays(1), data);
        await persister.GetBytes("msg1", "attachment", connection, null);
    }

    [Benchmark]
    public async Task SaveAndCopyTo()
    {
        var stream = new MemoryStream(data);
        await persister.SaveStream(
            connection, null, "msg1", "attachment", DateTime.UtcNow.AddDays(1), stream);
        await persister.CopyTo("msg1", "attachment", connection, null, Stream.Null);
    }

    [Benchmark]
    public async Task SaveAndGetStream()
    {
        var stream = new MemoryStream(data);
        await persister.SaveStream(
            connection, null, "msg1", "attachment", DateTime.UtcNow.AddDays(1), stream);
        await using var result = await persister.GetStream("msg1", "attachment", connection, null, false);
    }
}
