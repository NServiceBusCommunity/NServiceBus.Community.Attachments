namespace NServiceBus.Attachments
#if FileShare
    .FileShare
#elif Sql
    .Sql
#endif
;

class Outgoing
{
    public Encoding? Encoding { get; init; }
    public Func<Stream, Task>? StreamWriter { get; init; }
    public Func<Task<byte[]>>? AsyncBytesFactory { get; init; }
    public Func<byte[]>? BytesFactory { get; init; }
    public byte[]? BytesInstance { get; init; }
    public string? StringInstance { get; init; }
    public GetTimeToKeep? TimeToKeep { get; init; }
    public Action? Cleanup { get; init; }
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    public string? IncomingFromName { get; init; }
    public Func<Stream, Stream, Cancel, Task>? IncomingTransform { get; init; }
    public bool BufferSource { get; init; }
    public bool BufferSink { get; init; }

    // PreSaved: the attachment was written to storage by the handler (immediate-write API)
    // so SendBehavior must skip the save and only add it to the Attachments header.
    // PreSavedGuid is populated for Sql (becomes the row guid in the header); null for FileShare.
    public bool IsPreSaved { get; init; }
    public Guid? PreSavedGuid { get; init; }

    public bool HasString => StringInstance != null;
    public bool HasBytes => BytesInstance != null ||
                            BytesFactory != null ||
                            AsyncBytesFactory != null;
    public bool HasStreamWriter => StreamWriter != null;
    public bool HasIncomingTransform => IncomingTransform != null;
}
