public class PersisterTests
{
    DateTime defaultTestDate = new(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc);
    Dictionary<string, string> metadata = new() {{"key", "value"}};

    static async Task<(SqlDatabase database, Persister persister)> BuildDb([CallerMemberName] string name = "")
    {
        var database = await Connection.SqlInstance.Build(name);
        var dbName = new SqlConnectionStringBuilder(database.ConnectionString).InitialCatalog;
        return (database, new Persister(dbName));
    }

    [Test]
    public async Task CopyTo()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        await persister.SaveStream(connection, null, "theMessageId", "theName", defaultTestDate, GetStream());
        var memoryStream = new MemoryStream();
        await persister.CopyTo("theMessageId", "theName", connection, null, memoryStream);

        memoryStream.Position = 0;
        await Assert.That((int)memoryStream.GetBuffer()[0]).IsEqualTo(5);
    }

    [Test]
    public async Task GetBytes()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        await persister.SaveStream(connection, null, "theMessageId", "theName", defaultTestDate, GetStream(), metadata);
        byte[] bytes = await persister.GetBytes("theMessageId", "theName", connection, null);
        await Assert.That((int)bytes[0]).IsEqualTo(5);
    }

    [Test]
    public async Task GetMemoryStream()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        await persister.SaveStream(connection, null, "theMessageId", "theName", defaultTestDate, GetStream(), metadata);
        var bytes = await persister.GetMemoryStream("theMessageId", "theName", connection, null);
        await Assert.That(bytes.ReadByte()).IsEqualTo(5);
    }

    [Test]
    public async Task CaseInsensitiveRead()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        await persister.SaveStream(connection, null, "theMessageId", "theName", defaultTestDate, GetStream());
        byte[] bytes = await persister.GetBytes("themeSsageid", "Thename", connection, null);
        await Assert.That((int)bytes[0]).IsEqualTo(5);
    }

    [Test]
    public async Task LongName()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        var name = new string('a', 255);
        await persister.SaveStream(connection, null, "theMessageId", name, defaultTestDate, GetStream());
        byte[] bytes = await persister.GetBytes("theMessageId", name, connection, null);
        await Assert.That((int)bytes[0]).IsEqualTo(5);
    }

    [Test]
    public async Task ProcessStream()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        var count = 0;
        await persister.SaveStream(connection, null, "theMessageId", "theName", defaultTestDate, GetStream(), metadata);
        await persister.ProcessStream("theMessageId", "theName", connection, null,
            action: async (stream, _) =>
            {
                count++;
                var array = ToBytes(stream);
                await Assert.That((int)array[0]).IsEqualTo(5);
            });
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task ProcessByteArray()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        var count = 0;
        await persister.SaveStream(connection, null, "theMessageId", "theName", defaultTestDate, GetStream(), metadata);
        await persister.ProcessByteArray("theMessageId", "theName", connection, null,
            action: async (bytes, _) =>
            {
                count++;
                var array = bytes.Bytes;
                await Assert.That((int)array[0]).IsEqualTo(5);
            });
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task ProcessStreamMultiple()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        var count = 0;
        for (var i = 0; i < 10; i++)
        {
            await persister.SaveStream(connection, null, "theMessageId", $"theName{i}", defaultTestDate, GetStream(), metadata);
        }

        for (var i = 0; i < 10; i++)
        {
            await persister.ProcessStream("theMessageId", $"theName{i}", connection, null,
                action: async (stream, _) =>
                {
                    Interlocked.Increment(ref count);
                    var array = ToBytes(stream);
                    await Assert.That((int)array[0]).IsEqualTo(5);
                });
        }

        await Assert.That(count).IsEqualTo(10);
    }

    [Test]
    public async Task GetMultipleStreams()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        await persister.SaveStream(connection, null, "theMessageId", "theName1", defaultTestDate, GetStream(1), metadata);
        await persister.SaveStream(connection, null, "theMessageId", "theName2", defaultTestDate, GetStream(2), metadata);
        var names = new List<string>();
        await foreach (var attachment in persister.GetStreams("theMessageId", connection, null))
        {
            var array = ToBytes(attachment);
            names.Add(attachment.Name);
            await Assert.That(array[0] == 1 || array[0] == 2).IsTrue();
        }

        await Assert.That(names.SequenceEqual(["theName1", "theName2"])).IsTrue();
    }

    [Test]
    public async Task GetMultipleBytes()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        var names = new List<string>();
        await persister.SaveStream(connection, null, "theMessageId", "theName1", defaultTestDate, GetStream(1), metadata);
        await persister.SaveStream(connection, null, "theMessageId", "theName2", defaultTestDate, GetStream(2), metadata);
        await foreach (var attachment in persister.GetBytes("theMessageId", connection, null))
        {
            names.Add(attachment.Name);
            await Assert.That(attachment.Bytes[0] == 1 || attachment.Bytes[0] == 2).IsTrue();
        }

        await Assert.That(names.SequenceEqual(["theName1", "theName2"])).IsTrue();
    }

    [Test]
    public async Task GetMultipleWithPause()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        var names = new List<string>();
        await persister.SaveStream(connection, null, "theMessageId", "theName1", defaultTestDate, GetStream(1), metadata);
        await Task.Delay(1000);
        await persister.SaveStream(connection, null, "theMessageId", "theName2", defaultTestDate, GetStream(2), metadata);
        await foreach (var attachment in persister.GetBytes("theMessageId", connection, null))
        {
            names.Add(attachment.Name);
        }

        await Assert.That(names.SequenceEqual(["theName1", "theName2"])).IsTrue();
    }

    [Test]
    public async Task GetMultipleStrings()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        var names = new List<string>();
        await persister.SaveString(connection, null, "theMessageId", "theName1", defaultTestDate, "a", null, metadata);
        await persister.SaveString(connection, null, "theMessageId", "theName2", defaultTestDate, "b", null, metadata);
        await foreach (var attachment in persister.GetStrings("theMessageId", connection, null))
        {
            names.Add(attachment.Name);
            await Assert.That(attachment.Value is "a" or "b").IsTrue();
        }

        await Assert.That(names.SequenceEqual(["theName1", "theName2"])).IsTrue();
    }

    [Test]
    public async Task ProcessStreams()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        var count = 0;
        await persister.SaveStream(connection, null, "theMessageId", "theName1", defaultTestDate, GetStream(1), metadata);
        await persister.SaveStream(connection, null, "theMessageId", "theName2", defaultTestDate, GetStream(2), metadata);
        await persister.ProcessStreams("theMessageId", connection, null,
            action: async (stream, _) =>
            {
                count++;
                var array = ToBytes(stream);
                if (count == 1)
                {
                    await Assert.That((int)array[0]).IsEqualTo(1);
                    await Assert.That(stream.Name).IsEqualTo("theName1");
                }

                if (count == 2)
                {
                    await Assert.That((int)array[0]).IsEqualTo(2);
                    await Assert.That(stream.Name).IsEqualTo("theName2");
                }
            });
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task ProcessByteArrays()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        var count = 0;
        await persister.SaveStream(connection, null, "theMessageId", "theName1", defaultTestDate, GetStream(1), metadata);
        await persister.SaveStream(connection, null, "theMessageId", "theName2", defaultTestDate, GetStream(2), metadata);
        await persister.ProcessByteArrays("theMessageId", connection, null,
            action: async (array, _) =>
            {
                count++;
                var bytes = array.Bytes;
                if (count == 1)
                {
                    await Assert.That((int)bytes[0]).IsEqualTo(1);
                    await Assert.That(array.Name).IsEqualTo("theName1");
                }

                if (count == 2)
                {
                    await Assert.That((int)bytes[0]).IsEqualTo(2);
                    await Assert.That(array.Name).IsEqualTo("theName2");
                }
            });
        await Assert.That(count).IsEqualTo(2);
    }

    static byte[] ToBytes(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    [Test]
    public async Task SaveStream()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        await persister.SaveStream(connection, null, "theMessageId", "theName", defaultTestDate, GetStream(), metadata);
        var result = persister.ReadAllInfo(connection, null);
        await Verify(result);
    }

    [Test]
    public async Task SaveBytes()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        await persister.SaveBytes(connection, null, "theMessageId", "theName", defaultTestDate, [1], metadata);
        var result = persister.ReadAllInfo(connection, null);
        await Verify(result);
    }

    [Test]
    public async Task SaveString()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        await persister.SaveString(connection, null, "theMessageId", "theName", defaultTestDate, "foo", null, metadata);
        var result = persister.ReadAllInfo(connection, null);
        await Verify(result);
    }

    [Test]
    public async Task LargeString()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        var expected = new string('*', 100000);
        await persister.SaveString(connection, null, "theMessageId", "theName", defaultTestDate, expected, null, metadata);
        var result = await persister.GetString("theMessageId", "theName", connection, null);
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task DiffEncoding()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        var encoding = Encoding.BigEndianUnicode;
        await persister.SaveString(connection, null, "theMessageId", "theName", defaultTestDate, "Sample", encoding, metadata);

        var result = await persister.GetString("theMessageId", "theName", connection, null);
        var encodingName = result.Metadata["encoding"];
        await Assert.That(encoding.WebName).IsEqualTo(encodingName);
        await Assert.That(result).IsEqualTo("Sample");
    }

    [Test]
    public async Task DiffEncodingOverride()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        var encoding = Encoding.BigEndianUnicode;
        await persister.SaveString(connection, null, "theMessageId", "theName", defaultTestDate, "Sample", encoding, metadata);

        var result = await persister.GetString("theMessageId", "theName", connection, null, Encoding.BigEndianUnicode);
        var encodingName = result.Metadata["encoding"];
        await Assert.That(encoding.WebName).IsEqualTo(encodingName);
        await Assert.That(result).IsEqualTo("Sample");
    }

    [Test]
    public async Task SaveStringEncoding()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        var expected = "¡™£¢∞§¶•ªº–≠";
        var encoding = new UTF8Encoding(true);
        await persister.SaveString(connection, null, "theMessageId", "theName", defaultTestDate, expected, encoding, metadata);
        var result = await persister.GetString("theMessageId", "theName", connection, null, encoding);
        Trace.Write(result);
        var attachmentBytes = await persister.GetBytes("theMessageId", "theName", connection, null);
        var bytes = attachmentBytes.Bytes;
        await Assert.That(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF).IsTrue().Because("Expected a BOM");
        await Assert.That(bytes.SequenceEqual(expected.ToBytes(encoding))).IsTrue();
    }

    [Test]
    public async Task DuplicateAll()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        await persister.SaveBytes(connection, null, "theSourceMessageId", "theName1", defaultTestDate, [1], metadata);
        await persister.SaveBytes(connection, null, "theSourceMessageId", "theName2", defaultTestDate, [1], metadata);
        var names = await persister.Duplicate("theSourceMessageId", connection, null, "theTargetMessageId");
        var info = await persister.ReadAllInfo(connection, null);
        await Verify(
                new
                {
                    names,
                    info = info.Where(_ => _.MessageId == "theTargetMessageId").ToList()
                })
            .IgnoreMember("Created");
    }

    [Test]
    public async Task Duplicate()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;

        await persister.SaveBytes(connection, null, "theSourceMessageId", "sourceName", defaultTestDate, [1], metadata);
        Thread.Sleep(1000); // Ensure different Created time
        await persister.Duplicate("theSourceMessageId", "sourceName", connection, null, "theTargetMessageId");

        // Add a second attachment for the same message
        await persister.SaveBytes(connection, null, "theSourceMessageId", "otherName", defaultTestDate, [1], metadata);

        var info = await persister.ReadAllInfo(connection, null);
        var sourceInfo = info.Single(_ => _ is { Name: "sourceName", MessageId: "theSourceMessageId" });
        var duplicateInfo = info.Single(_ => _.MessageId == "theTargetMessageId");

        await Assert.That(duplicateInfo.Expiry).IsEqualTo(sourceInfo.Expiry);
        await Verify(info.Where(_ => _.MessageId == "theTargetMessageId"))
            .IgnoreMember("Created");
    }

    [Test]
    public async Task DuplicateWithRename()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        await persister.SaveBytes(connection, null, "theSourceMessageId", "theName1", defaultTestDate, [1], metadata);
        Thread.Sleep(1000); // Ensure different Created time
        await persister.Duplicate("theSourceMessageId", "theName1", connection, null, "theTargetMessageId", "theName2");
        var info = await persister.ReadAllInfo(connection, null);
        await Assert.That(info[1].Expiry).IsEqualTo(info[0].Expiry);
        await Verify(info.Where(_ => _.MessageId == "theTargetMessageId"))
            .IgnoreMember("Created");
    }

    [Test]
    public async Task ReadAllMessageInfoAction()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        await persister.SaveBytes(connection, null, "theMessageId", "theName1", defaultTestDate, [1], metadata);
        await persister.SaveBytes(connection, null, "theMessageId", "theName2", defaultTestDate, [1], metadata);
        var list = new List<AttachmentInfo>();
        await persister.ReadAllMessageInfo(connection, null, "theMessageId",
            (info, _) =>
            {
                list.Add(info);
                return Task.CompletedTask;
            });
        await Verify(list)
            .IgnoreMember("Created");
    }

    [Test]
    public async Task ReadAllMessageInfo()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        await persister.SaveBytes(connection, null, "theMessageId", "theName1", defaultTestDate, [1], metadata);
        await persister.SaveBytes(connection, null, "theMessageId", "theName2", defaultTestDate, [1], metadata);
        await Verify(persister.ReadAllMessageInfo(connection, null, "theMessageId"))
            .IgnoreMember("Created");
    }

    [Test]
    public async Task ReadAllMessageNames()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        await persister.SaveBytes(connection, null, "theMessageId", "theName1", defaultTestDate, [1], metadata);
        await persister.SaveBytes(connection, null, "theMessageId", "theName2", defaultTestDate, [1], metadata);
        await Verify(persister.ReadAllMessageNames(connection, null, "theMessageId"));
    }

    [Test]
    public async Task CleanupItemsOlderThan()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        await persister.SaveStream(connection, null, "theMessageId1", "theName", defaultTestDate, GetStream());
        await persister.SaveStream(connection, null, "theMessageId2", "theName", defaultTestDate.AddYears(2), GetStream());
        var cleanupCount = await persister.CleanupItemsOlderThan(connection, null, new(2001, 1, 1, 1, 1, 1));
        var result = await persister.ReadAllInfo(connection, null);
        await Verify(new {cleanupCount, result});
    }

    [Test]
    public async Task PurgeItems()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        await persister.SaveStream(connection, null, "theMessageId1", "theName1", defaultTestDate, GetStream());
        await persister.SaveStream(connection, null, "theMessageId1", "theName2", defaultTestDate, GetStream());
        await persister.SaveStream(connection, null, "theMessageId2", "theName", defaultTestDate, GetStream());
        var purgeCount = await persister.PurgeItems(connection, null);
        var result = await persister.ReadAllInfo(connection, null);
        await Verify(
            new
            {
                result,
                purgeCount
            });
    }

    [Test]
    public async Task DeleteAttachments()
    {
        var (database, persister) = await BuildDb();
        await using var _ = database;
        var connection = database.Connection;
        await persister.SaveStream(connection, null, "theMessageId1", "theName1", defaultTestDate, GetStream());
        await persister.SaveStream(connection, null, "theMessageId1", "theName2", defaultTestDate, GetStream());
        await persister.SaveStream(connection, null, "theMessageId2", "theName", defaultTestDate, GetStream());
        var deleteCount = await persister.DeleteAttachments("theMessageId1", connection, null);
        var result = await persister.ReadAllInfo(connection, null);
        await Verify(
            new
            {
                result,
                deleteCount
            });
    }

    static Stream GetStream(byte content = 5)
    {
        var stream = new MemoryStream();
        stream.WriteByte(content);
        stream.Position = 0;
        return stream;
    }
}
