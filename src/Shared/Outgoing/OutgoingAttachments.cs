#if FileShare
using NServiceBus.Attachments.FileShare;
#elif Sql
using NServiceBus.Attachments.Sql;
#else
using NServiceBus.Attachments;
#endif

class OutgoingAttachments :
    IOutgoingAttachments
{
    internal Dictionary<string, Outgoing> Inner = new(StringComparer.OrdinalIgnoreCase);
    public List<Duplicate> Duplicates = [];

    public bool HasPendingAttachments =>
        Inner.Count != 0 ||
        DuplicateIncomingAttachments ||
        Duplicates.Count != 0 ||
        Dynamic.Count != 0;

    public bool DuplicateIncomingAttachments;

    public IReadOnlyList<OutgoingAttachment> Items =>
        Inner
            .Select(_ =>
                new OutgoingAttachment
                {
                    Name = _.Key,
                    Metadata = _.Value.Metadata,
                    Encoding = _.Value.Encoding
                })
            .ToList();

    internal List<AttachmentFactory> Dynamic = [];

    public void Add(AttachmentFactory factory) =>
        Dynamic.Add(factory);

    public void AddStream(Func<Stream, Task> writer, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null) =>
        AddStream("default", writer, timeToKeep, cleanup, metadata);

    public void AddStream(string name, Func<Stream, Task> writer, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null) =>
        Inner.Add(
            name,
            new()
            {
                Metadata = metadata,
                TimeToKeep = timeToKeep,
                Cleanup = cleanup.WrapCleanupInCheck(name),
                StreamWriter = writer.WrapStreamWriterInCheck(name)
            });

    public void Add(Stream stream, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null) =>
        Add("default", stream, timeToKeep, cleanup, metadata);

    public void Add(string name, Stream stream, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null) =>
        AddStream(name, stream.CopyToAsync, timeToKeep, cleanup, metadata);

    public void AddBytes(Func<byte[]> bytesFactory, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null) =>
        AddBytes("default", bytesFactory, timeToKeep, cleanup, metadata);

    public void AddBytes(byte[] bytes, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null) =>
        AddBytes("default", bytes, timeToKeep, cleanup, metadata);

    public void DuplicateIncoming() =>
        DuplicateIncomingAttachments = true;

    public void DuplicateIncoming(string incomingName, string? outgoingName = null) =>
        Duplicates.Add(new(from: incomingName, to: outgoingName));

    public void AddBytes(string name, Func<byte[]> bytesFactory, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null) =>
        Inner.Add(
            name,
            new()
            {
                Metadata = metadata,
                TimeToKeep = timeToKeep,
                Cleanup = cleanup.WrapCleanupInCheck(name),
                BytesFactory = bytesFactory.WrapFuncInCheck(name)
            });

    public void AddBytes(string name, byte[] bytes, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null) =>
        Inner.Add(
            name,
            new()
            {
                Metadata = metadata,
                TimeToKeep = timeToKeep,
                Cleanup = cleanup.WrapCleanupInCheck(name),
                BytesInstance = bytes,
            });

    public void AddString(string value, Encoding? encoding, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null) =>
        AddString("default", value, encoding, timeToKeep, cleanup, metadata);

    public void AddString(string name, string value, Encoding? encoding, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null) =>
        Inner.Add(
            name,
            new()
            {
                Metadata = metadata,
                TimeToKeep = timeToKeep,
                Cleanup = cleanup.WrapCleanupInCheck(name),
                Encoding = encoding,
                StringInstance = value,
            });

    public void AddBytes(Func<Task<byte[]>> bytesFactory, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null) =>
        AddBytes("default", bytesFactory, timeToKeep, cleanup, metadata);

    public void AddBytes(string name, Func<Task<byte[]>> bytesFactory, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null) =>
        Inner.Add(
            name,
            new()
            {
                Metadata = metadata,
                Cleanup = cleanup.WrapCleanupInCheck(name),
                AsyncBytesFactory = bytesFactory.WrapFuncTaskInCheck(name)
            });
}