public class PersisterTests
{
    DateTime defaultTestDate = new(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc);
    Dictionary<string, string> metadata = new()
        {{"key", "value"}};

    static Persister GetPersister([CallerMemberName] string? path = null)
    {
        var fileShare = Path.GetFullPath($"attachments/{path}");
        var persister = new Persister(fileShare);
        Directory.CreateDirectory(fileShare);
        persister.DeleteAllAttachments();
        return persister;
    }

    [Test]
    public async Task CopyTo()
    {
        var persister = GetPersister();
        await persister.SaveStream("theMessageId", "theName", defaultTestDate, GetStream());
        var memoryStream = new MemoryStream();
        await persister.CopyTo("theMessageId", "theName", memoryStream);

        memoryStream.Position = 0;
        await Assert.That((int)memoryStream.GetBuffer()[0]).IsEqualTo(5);
    }

    [Test]
    public async Task GetBytes()
    {
        var persister = GetPersister();
        await persister.SaveStream("theMessageId", "theName", defaultTestDate, GetStream(), metadata);
        byte[] bytes = await persister.GetBytes("theMessageId", "theName");
        await Assert.That((int)bytes[0]).IsEqualTo(5);
    }

    [Test]
    public async Task GetMemoryStream()
    {
        var persister = GetPersister();
        await persister.SaveStream("theMessageId", "theName", defaultTestDate, GetStream(), metadata);
        var bytes = await persister.GetMemoryStream("theMessageId", "theName");
        await Assert.That(bytes.ReadByte()).IsEqualTo(5);
    }

    [Test]
    public async Task CaseInsensitiveRead()
    {
        var persister = GetPersister();
        await persister.SaveStream("theMessageId", "theName", defaultTestDate, GetStream());
        byte[] bytes = await persister.GetBytes("themeSsageid", "Thename");
        await Assert.That((int)bytes[0]).IsEqualTo(5);
    }

    [Test]
    public async Task ProcessStream()
    {
        var persister = GetPersister();
        var count = 0;
        await persister.SaveStream("theMessageId", "theName", defaultTestDate, GetStream());
        await persister.ProcessStream("theMessageId", "theName",
            action: async (stream, _) =>
            {
                count++;
                var array = ToBytes(stream);
                await Assert.That((int)array[0]).IsEqualTo(5);
            });
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task ProcessStreams()
    {
        var persister = GetPersister();
        var count = 0;
        await persister.SaveStream("theMessageId", "theName1", defaultTestDate, GetStream(1));
        await persister.SaveStream("theMessageId", "theName2", defaultTestDate, GetStream(2));
        await persister.ProcessStreams("theMessageId",
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
    public async Task GetMultipleStreams()
    {
        var persister = GetPersister();
        var count = 0;
        await persister.SaveStream("theMessageId", "theName1", defaultTestDate, GetStream(1), metadata);
        await persister.SaveStream("theMessageId", "theName2", defaultTestDate, GetStream(2), metadata);
        await foreach (var attachment in persister.GetStreams("theMessageId"))
        {
            var array = ToBytes(attachment);
            await Assert.That(attachment.Name is "theName1" or "theName2").IsTrue();
            await Assert.That(array[0] == 1 || array[0] == 2).IsTrue();
            Interlocked.Increment(ref count);
        }

        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task GetMultipleBytes()
    {
        var persister = GetPersister();
        var count = 0;
        await persister.SaveStream("theMessageId", "theName1", defaultTestDate, GetStream(1), metadata);
        await persister.SaveStream("theMessageId", "theName2", defaultTestDate, GetStream(2), metadata);
        await foreach (var attachment in persister.GetBytes("theMessageId"))
        {
            await Assert.That(attachment.Name is "theName1" or "theName2").IsTrue();
            await Assert.That(attachment.Bytes[0] == 1 || attachment.Bytes[0] == 2).IsTrue();
            Interlocked.Increment(ref count);
        }

        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task GetMultipleStrings()
    {
        var persister = GetPersister();
        var count = 0;
        await persister.SaveString("theMessageId", "theName1", defaultTestDate, "a", null, metadata);
        await persister.SaveString("theMessageId", "theName2", defaultTestDate, "b", null, metadata);
        await foreach (var attachment in persister.GetStrings("theMessageId"))
        {
            await Assert.That(attachment.Name is "theName1" or "theName2").IsTrue();
            await Assert.That(attachment.Value is "a" or "b").IsTrue();
            Interlocked.Increment(ref count);
        }

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
        var persister = GetPersister();
        await persister.SaveStream(
            messageId: "theMessageId", "theName",
            expiry: defaultTestDate,
            stream: GetStream(),
            metadata: metadata);
        await Verify(persister.ReadAllInfo());
    }

    [Test]
    public async Task ReadAllMessageNames()
    {
        var persister = GetPersister();
        await persister.SaveStream("theMessageId", "theName1", defaultTestDate, GetStream(), metadata);
        await persister.SaveStream("theMessageId", "theName2", defaultTestDate, GetStream(), metadata);
        await Verify(persister.ReadAllMessageNames("theMessageId"));
    }

    [Test]
    public async Task ReadAllMessageInfo()
    {
        var persister = GetPersister();
        await persister.SaveStream("theMessageId", "theName1", defaultTestDate, GetStream(), metadata);
        await persister.SaveStream("theMessageId", "theName2", defaultTestDate, GetStream(), metadata);
        await Verify(persister.ReadAllMessageInfo("theMessageId"))
            .IgnoreMember("Created");
    }

    [Test]
    public async Task SaveBytes()
    {
        var persister = GetPersister();
        await persister.SaveBytes("theMessageId", "theName", defaultTestDate, [1], metadata);
        await Verify(persister.ReadAllInfo());
    }

    [Test]
    public async Task SaveString()
    {
        var persister = GetPersister();
        await persister.SaveString("theMessageId", "theName", defaultTestDate, "foo", null, metadata);
        await Verify(persister.ReadAllInfo());
    }

    [Test]
    public async Task SaveStringEncoding()
    {
        var persister = GetPersister();
        var expected = "¡™£¢∞§¶•ªº–≠";
        var encoding = new UTF8Encoding(true);
        await persister.SaveString("theMessageId", "theName", defaultTestDate, expected, encoding, metadata);
        var result = await persister.GetString("theMessageId", "theName", encoding);
        var attachmentBytes = await persister.GetBytes("theMessageId", "theName");
        var bytes = attachmentBytes.Bytes;
        await Assert.That(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF).IsTrue().Because("Expected a BOM");
        await Assert.That(result.Value).IsEqualTo(expected);
    }

    [Test]
    public async Task DiffEncoding()
    {
        var persister = GetPersister();
        var encoding = Encoding.BigEndianUnicode;
        await persister.SaveString("theMessageId", "theName", defaultTestDate, "Sample", encoding, metadata);
        var result = await persister.GetString("theMessageId", "theName", null);
        var encodingName = result.Metadata["encoding"];
        await Assert.That(encoding.WebName).IsEqualTo(encodingName);
        await Assert.That(result).IsEqualTo("Sample");
    }

    [Test]
    public async Task DiffEncodingOverride()
    {
        var persister = GetPersister();
        var encoding = Encoding.BigEndianUnicode;
        await persister.SaveString("theMessageId", "theName", defaultTestDate, "Sample", encoding, metadata);
        var result = await persister.GetString("theMessageId", "theName", Encoding.Latin1);
        var encodingName = result.Metadata["encoding"];
        await Assert.That(encoding.WebName).IsEqualTo(encodingName);
        await Assert.That(result).IsEqualTo("Sample");
    }

    [Test]
    public async Task DuplicateAll()
    {
        var persister = GetPersister();
        await persister.SaveStream("theSourceMessageId", "theName1", defaultTestDate, GetStream(), metadata);
        await persister.SaveStream("theSourceMessageId", "theName2", defaultTestDate, GetStream(), metadata);
        var names = await persister.Duplicate("theSourceMessageId", "theTargetMessageId");
        var info = await persister
            .ReadAllInfo()
            .ToAsyncList();
        await Assert.That(info[2].Created).IsEqualTo(info[0].Created);
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
        var persister = GetPersister();
        await persister.SaveStream("theSourceMessageId", "theName1", defaultTestDate, GetStream(), metadata);
        await persister.SaveStream("theSourceMessageId", "theName2", defaultTestDate, GetStream(), metadata);
        await persister.Duplicate("theSourceMessageId", "theName1", "theTargetMessageId");
        var info = await persister
            .ReadAllInfo()
            .ToAsyncList();
        await Assert.That(info[2].Created).IsEqualTo(info[0].Created);
        await Verify(info.Where(_ => _.MessageId == "theTargetMessageId"))
            .IgnoreMember("Created");
    }

    [Test]
    public async Task DuplicateWithRename()
    {
        var persister = GetPersister();
        await persister.SaveStream("theSourceMessageId", "theName1", defaultTestDate, GetStream(), metadata);
        await persister.Duplicate("theSourceMessageId", "theName1", "theTargetMessageId", "theName2");
        var info = await persister
            .ReadAllInfo()
            .ToAsyncList();
        await Assert.That(info[1].Created).IsEqualTo(info[0].Created);
        await Verify(info.Where(_ => _.MessageId == "theTargetMessageId"))
            .IgnoreMember("Created");
    }

    [Test]
    public async Task CleanupItemsOlderThan()
    {
        var persister = GetPersister();
        await persister.SaveStream("theMessageId1", "theName", defaultTestDate, GetStream());
        await persister.SaveStream("theMessageId2", "theName", defaultTestDate.AddYears(2), GetStream());
        persister.CleanupItemsOlderThan(new(2001, 1, 1, 1, 1, 1));
        await Verify(persister.ReadAllInfo());
    }

    static Stream GetStream(byte content = 5)
    {
        var stream = new MemoryStream();
        stream.WriteByte(content);
        stream.Position = 0;
        return stream;
    }
}
