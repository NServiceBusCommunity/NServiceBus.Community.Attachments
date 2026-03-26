using System.IO.Pipelines;
using BenchmarkDotNet.Attributes;
using LocalDb;
using Microsoft.Data.SqlClient;
using NServiceBus.Attachments.Sql;

[MemoryDiagnoser]
[GcServer(true)]
public class PersisterBenchmarks
{
    static SqlInstance sqlInstance = null!;
    SqlDatabase database = null!;
    SqlConnection connection = null!;
    Persister persister = null!;
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

    byte[] NewData()
    {
        var data = new byte[DataSize];
        Random.Shared.NextBytes(data);
        return data;
    }

    [IterationSetup]
    public void IterationSetup()
    {
        database = sqlInstance.Build($"Bench{Interlocked.Increment(ref counter)}").GetAwaiter().GetResult();
        connection = database.Connection;
        var dbName = new SqlConnectionStringBuilder(database.ConnectionString).InitialCatalog;
        persister = new(dbName);
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
    public Task<Guid> SaveStream()
    {
        var data = NewData();
        var stream = new MemoryStream(data);
        return persister.SaveStream(
            connection, null, "msg1", "attachment", DateTime.UtcNow.AddDays(1), stream, null);
    }

    [Benchmark]
    public Task<Guid> SaveBytes()
    {
        var data = NewData();
        return persister.SaveBytes(
            connection, null, "msg1", "attachment", DateTime.UtcNow.AddDays(1), data, null);
    }

    [Benchmark]
    public async Task SaveViaPipe()
    {
        var pipe = new Pipe(new(pauseWriterThreshold: 65536, resumeWriterThreshold: 32768));
        var capturedData = NewData();
        var writerTask = Task.Run(async () =>
        {
            try
            {
                var memory = capturedData.AsMemory();
                const int chunkSize = 8192;
                for (var offset = 0; offset < memory.Length; offset += chunkSize)
                {
                    var chunk = memory.Slice(offset, Math.Min(chunkSize, memory.Length - offset));
                    var buffer = pipe.Writer.GetMemory(chunk.Length);
                    chunk.CopyTo(buffer);
                    pipe.Writer.Advance(chunk.Length);
                    await pipe.Writer.FlushAsync();
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
                connection, null, "msg1", "attachment", DateTime.UtcNow.AddDays(1), readerStream, null);
            await writerTask;
        }
    }

    [Benchmark]
    public async Task SaveAndGetBytes()
    {
        var data = NewData();
        await persister.SaveBytes(
            connection, null, "msg1", "attachment", DateTime.UtcNow.AddDays(1), data, null);
        await persister.GetBytes("msg1", "attachment", connection, null);
    }

    [Benchmark]
    public async Task SaveAndCopyTo()
    {
        var data = NewData();
        var stream = new MemoryStream(data);
        await persister.SaveStream(
            connection, null, "msg1", "attachment", DateTime.UtcNow.AddDays(1), stream, null);
        await persister.CopyTo("msg1", "attachment", connection, null, Stream.Null);
    }

    [Benchmark]
    public async Task SaveAndGetStream()
    {
        var data = NewData();
        var stream = new MemoryStream(data);
        await persister.SaveStream(
            connection, null, "msg1", "attachment", DateTime.UtcNow.AddDays(1), stream, null);
        await using var result = await persister.GetStream("msg1", "attachment", connection, null, false);
    }
}
