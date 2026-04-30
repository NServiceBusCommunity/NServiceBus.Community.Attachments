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

    public bool HasString => StringInstance != null;
    public bool HasBytes => BytesInstance != null ||
                            BytesFactory != null ||
                            AsyncBytesFactory != null;
    public bool HasStreamWriter => StreamWriter != null;
    public bool HasIncomingTransform => IncomingTransform != null;
}
