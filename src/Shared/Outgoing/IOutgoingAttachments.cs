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
    void AddStream(Func<Stream, Task> writer, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null);

    /// <summary>
    /// Add an attachment with <paramref name="name"/> using a push-based stream writer delegate.
    /// Data is streamed directly to storage using System.IO.Pipelines without intermediate buffering.
    /// Use for large payloads or when data is generated/read incrementally.
    /// </summary>
    void AddStream(string name, Func<Stream, Task> writer, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null);

    /// <summary>
    /// Add an attachment with the default name using a synchronous push-based stream writer delegate.
    /// Data is streamed directly to storage using System.IO.Pipelines without intermediate buffering.
    /// </summary>
    void AddStream(Action<Stream> writer, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null);

    /// <summary>
    /// Add an attachment with <paramref name="name"/> using a synchronous push-based stream writer delegate.
    /// Data is streamed directly to storage using System.IO.Pipelines without intermediate buffering.
    /// </summary>
    void AddStream(string name, Action<Stream> writer, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null);

    /// <summary>
    /// Add an attachment with <paramref name="name"/> to the current outgoing pipeline.
    /// </summary>
    void Add(string name, Stream stream, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null);

    /// <summary>
    /// Add an attachment with the default name to the current outgoing pipeline.
    /// </summary>
    void Add(Stream stream, GetTimeToKeep? timeToKeep = null, Action? cleanup = null, IReadOnlyDictionary<string, string>? metadata = null);

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

    /// <summary>
    /// Add an outgoing attachment whose data is produced by transforming the incoming attachment of <paramref name="fromName"/>
    /// for the current message. The library opens the incoming read inside the outgoing pipeline so the source and sink
    /// streams are live at the same time and no intermediate buffering by the caller is needed.
    /// </summary>
    /// <param name="fromName">Name of the incoming attachment to read.</param>
    /// <param name="toName">Name to assign to the produced outgoing attachment.</param>
    /// <param name="transform">Delegate that reads from <c>source</c> and writes to <c>sink</c>.</param>
    /// <param name="bufferSource">If true, the incoming data is buffered to a seekable <see cref="MemoryStream"/> before <paramref name="transform"/> runs. Use when the transform requires seek/Position on the input (e.g. email/MIME parsers).</param>
    /// <param name="bufferSink">If true, <paramref name="transform"/> writes to a seekable <see cref="MemoryStream"/> which is then drained to storage. Use when the transform requires seek/Position on the output (e.g. Aspose.Slides).</param>
    /// <param name="timeToKeep">How long the produced outgoing attachment should be retained.</param>
    /// <param name="cleanup">Optional cleanup callback invoked after the attachment has been processed.</param>
    /// <param name="metadata">Optional metadata stored alongside the produced outgoing attachment.</param>
    void AddFromIncoming(
        string fromName,
        string toName,
        Func<Stream, Stream, Cancel, Task> transform,
        bool bufferSource = false,
        bool bufferSink = false,
        GetTimeToKeep? timeToKeep = null,
        Action? cleanup = null,
        IReadOnlyDictionary<string, string>? metadata = null);
}
