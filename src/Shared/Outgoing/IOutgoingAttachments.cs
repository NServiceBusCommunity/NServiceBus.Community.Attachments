namespace NServiceBus.Attachments
#if FileShare
    .FileShare
#elif Sql
    .Sql
#endif
;

public delegate Task AttachmentFactory(AppendAttachment append);
public delegate Task AppendAttachment(string name, Stream stream, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null);

/// <summary>
/// Provides access to write attachments.
/// </summary>
public interface IOutgoingAttachments
{
    /// <summary>
    /// Returns <code>true</code> if there are pending attachments to be written in the current outgoing pipeline.
    /// </summary>
    bool HasPendingAttachments { get; }

    IReadOnlyList<OutgoingAttachment> Items { get; }

    /// <summary>
    /// Add attachments dynamically to the current outgoing pipeline.
    /// Use when the number of attachments is not known at compile time.
    /// </summary>
    void Add(AttachmentFactory factory);

    /// <summary>
    /// Add an attachment with the default name using a push-based stream writer delegate.
    /// Data is streamed directly to storage using System.IO.Pipelines without intermediate buffering.
    /// Use for large payloads or when data is generated/read incrementally.
    /// </summary>
    void AddStreamWriter(Func<Stream, Task> streamWriter, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null);

    /// <summary>
    /// Add an attachment with <paramref name="name"/> using a push-based stream writer delegate.
    /// Data is streamed directly to storage using System.IO.Pipelines without intermediate buffering.
    /// Use for large payloads or when data is generated/read incrementally.
    /// </summary>
    void AddStreamWriter(string name, Func<Stream, Task> streamWriter, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null);

    /// <summary>
    /// Add an attachment with <paramref name="name"/> to the current outgoing pipeline.
    /// </summary>
    /// <remarks>
    /// Use for small payloads where the full data is already in memory.
    /// </remarks>
    void AddBytes(string name, Func<byte[]> byteFactory, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null);

    /// <summary>
    /// Add an attachment with <paramref name="name"/> to the current outgoing pipeline.
    /// </summary>
    /// <remarks>
    /// Use for small payloads where the full data is already in memory.
    /// </remarks>
    void AddBytes(string name, byte[] bytes, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null);

    /// <summary>
    /// Add an attachment with <paramref name="name"/> to the current outgoing pipeline.
    /// </summary>
    /// <remarks>
    /// Use for small payloads where the full data is already in memory.
    /// </remarks>
    void AddString(string name, string value, Encoding? encoding = null, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null);

    /// <summary>
    /// Add an attachment with the default name to the current outgoing pipeline.
    /// </summary>
    /// <remarks>
    /// Use for small payloads where the full data is already in memory.
    /// </remarks>
    void AddBytes(Func<Task<byte[]>> bytesFactory, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null);

    /// <summary>
    /// Add an attachment with <paramref name="name"/> to the current outgoing pipeline.
    /// </summary>
    /// <remarks>
    /// Use for small payloads where the full data is already in memory.
    /// </remarks>
    void AddBytes(string name, Func<Task<byte[]>> bytesFactory, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null);

    /// <summary>
    /// Add an attachment with the default name to the current outgoing pipeline.
    /// </summary>
    /// <remarks>
    /// Use for small payloads where the full data is already in memory.
    /// </remarks>
    void AddBytes(Func<byte[]> byteFactory, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null);

    /// <summary>
    /// Add an attachment with the default name to the current outgoing pipeline.
    /// </summary>
    /// <remarks>
    /// Use for small payloads where the full data is already in memory.
    /// </remarks>
    void AddBytes(byte[] bytes, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null);

    /// <summary>
    /// Add an attachment with the default name to the current outgoing pipeline.
    /// </summary>
    /// <remarks>
    /// Use for small payloads where the full data is already in memory.
    /// </remarks>
    void AddString(string value, Encoding? encoding = null, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null);

    /// <summary>
    /// Duplicates the incoming attachments to the current outgoing pipeline.
    /// </summary>
    void DuplicateIncoming();

    /// <summary>
    /// Duplicates the incoming attachments to the current outgoing pipeline.
    /// </summary>
    void DuplicateIncoming(string fromName, string toName);
}
