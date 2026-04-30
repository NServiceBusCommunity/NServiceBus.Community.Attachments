namespace NServiceBus.Attachments.FileShare
#if Raw
    .Raw
#endif
    ;

/// <summary>
/// Raw access to manipulating attachments outside of the context of the NServiceBus pipeline.
/// </summary>
public interface IPersister
{
    /// <summary>
    /// Saves <paramref name="stream" /> as an attachment.
    /// </summary>
    /// <exception cref="TaskCanceledException">If <paramref name="cancel" /> is <see cref="Cancel.IsCancellationRequested" />.</exception>
    Task SaveStream(string messageId, string name, DateTime expiry, Stream stream, IReadOnlyDictionary<string, string>? metadata, Cancel cancel = default);

    /// <summary>
    /// Opens a writable <see cref="Stream"/> for an attachment. Caller writes data and disposes to commit.
    /// </summary>
    Task<Stream> OpenSaveStream(string messageId, string name, DateTime expiry, IReadOnlyDictionary<string, string>? metadata = null, Cancel cancel = default);

    /// <summary>
    /// Saves <paramref name="value" /> as an attachment.
    /// </summary>
    /// <exception cref="TaskCanceledException">If <paramref name="cancel" /> is <see cref="Cancel.IsCancellationRequested" />.</exception>
    Task SaveString(string messageId, string name, DateTime expiry, string value, Encoding? encoding, IReadOnlyDictionary<string, string>? metadata, Cancel cancel = default);

    /// <summary>
    /// Reads the <see cref="AttachmentInfo" /> for all attachments.
    /// </summary>
    IAsyncEnumerable<AttachmentInfo> ReadAllInfo(Cancel cancel = default);

    /// <summary>
    /// Reads the names for all attachments of a specific message.
    /// </summary>
    IEnumerable<string> ReadAllMessageNames(string messageId);

    /// <summary>
    /// Reads the <see cref="AttachmentInfo" /> for all attachments of a specific message.
    /// </summary>
    IAsyncEnumerable<AttachmentInfo> ReadAllMessageInfo(string messageId, Cancel cancel = default);

    /// <summary>
    /// Deletes all attachments.
    /// </summary>
    void DeleteAllAttachments();

    /// <summary>
    /// Deletes attachments older than <paramref name="dateTime" />.
    /// </summary>
    void CleanupItemsOlderThan(DateTime dateTime, Cancel cancel = default);

    /// <summary>
    /// Deletes all items.
    /// </summary>
    void PurgeItems(Cancel cancel = default);

    /// <summary>
    /// Copies an attachment to <paramref name="target" />.
    /// </summary>
    Task CopyTo(string messageId, string name, Stream target, Cancel cancel = default);

    /// <summary>
    /// Copies an attachment to a different message.
    /// </summary>
    Task Duplicate(string sourceMessageId, string name, string targetMessageId, Cancel cancel = default);

    /// <summary>
    /// Copies an attachment to a different message.
    /// </summary>
    Task Duplicate(string sourceMessageId, string name, string targetMessageId, string targetName, Cancel cancel = default);

    /// <summary>
    /// Copies attachments to a different message.
    /// </summary>
    Task<IReadOnlyList<string>> Duplicate(string sourceMessageId, string targetMessageId, Cancel cancel = default);

    /// <summary>
    /// Reads an <see cref="AttachmentBytes" /> for an attachment.
    /// </summary>
    Task<AttachmentBytes> GetBytes(string messageId, string name, Cancel cancel = default);

    /// <summary>
    /// Reads an <see cref="AttachmentString" /> for an attachment.
    /// </summary>
    Task<AttachmentString> GetString(string messageId, string name, Encoding? encoding, Cancel cancel = default);

    /// <summary>
    /// Reads an <see cref="AttachmentStream" /> an attachment.
    /// </summary>
    Task<AttachmentStream> GetStream(string messageId, string name, Cancel cancel = default);

    /// <summary>
    /// Reads an <see cref="AttachmentStream" /> an attachment.
    /// </summary>
    Task<MemoryStream> GetMemoryStream(string messageId, string name, Cancel cancel = default);

    /// <summary>
    /// Processes all attachments for <paramref name="messageId" /> by passing them to <paramref name="action" />.
    /// </summary>
    Task ProcessStreams(string messageId, Func<AttachmentStream, Cancel, Task> action, Cancel cancel = default);

    /// <summary>
    /// Processes an attachment by passing it to <paramref name="action" />.
    /// </summary>
    Task ProcessStream(string messageId, string name, Func<AttachmentStream, Cancel, Task> action, Cancel cancel = default);

    /// <summary>
    /// Reads all <see cref="AttachmentBytes" />s for an attachment.
    /// </summary>
    IAsyncEnumerable<AttachmentBytes> GetBytes(string messageId, Cancel cancel = default);

    /// <summary>
    /// Reads all <see cref="AttachmentString" />s for an attachment.
    /// </summary>
    IAsyncEnumerable<AttachmentString> GetStrings(string messageId, Encoding? encoding, Cancel cancel = default);

    /// <summary>
    /// Reads all <see cref="AttachmentStream" />s to an attachment.
    /// </summary>
    IAsyncEnumerable<AttachmentStream> GetStreams(string messageId, Cancel cancel = default);
}