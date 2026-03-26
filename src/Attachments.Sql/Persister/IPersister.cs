using Microsoft.Data.SqlClient;

namespace NServiceBus.Attachments.Sql
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
    Task<Guid> SaveStream(SqlConnection connection, SqlTransaction? transaction, string messageId, string name, DateTime expiry, Stream stream, IReadOnlyDictionary<string, string>? metadata, Cancel cancel = default);

    /// <summary>
    /// Saves <paramref name="value" /> as an attachment.
    /// </summary>
    /// <exception cref="TaskCanceledException">If <paramref name="cancel" /> is <see cref="Cancel.IsCancellationRequested" />.</exception>
    Task<Guid> SaveString(SqlConnection connection, SqlTransaction? transaction, string messageId, string name, DateTime expiry, string value, Encoding? encoding, IReadOnlyDictionary<string, string>? metadata, Cancel cancel = default);

    /// <summary>
    /// Reads the <see cref="AttachmentInfo" /> for all attachments of a specific message.
    /// </summary>
    Task ReadAllMessageInfo(SqlConnection connection, SqlTransaction? transaction, string messageId, Func<AttachmentInfo, Cancel, Task> action, Cancel cancel = default);

    /// <summary>
    /// Reads the <see cref="AttachmentInfo" /> for all attachments of a specific message.
    /// </summary>
    IAsyncEnumerable<AttachmentInfo> ReadAllMessageInfo(SqlConnection connection, SqlTransaction? transaction, string messageId, Cancel cancel = default);

    /// <summary>
    /// Reads the names for all attachments of a specific message.
    /// </summary>
    IAsyncEnumerable<(Guid, string)> ReadAllMessageNames(SqlConnection connection, SqlTransaction? transaction, string messageId, Cancel cancel = default);

    /// <summary>
    /// Reads the <see cref="AttachmentInfo" /> for all attachments.
    /// </summary>
    Task ReadAllInfo(SqlConnection connection, SqlTransaction? transaction, Func<AttachmentInfo, Cancel, Task> action, Cancel cancel = default);

    /// <summary>
    /// Reads the <see cref="AttachmentInfo" /> for all attachments.
    /// </summary>
    Task<IReadOnlyList<AttachmentInfo>> ReadAllInfo(SqlConnection connection, SqlTransaction transaction, Cancel cancel = default);

    /// <summary>
    /// Deletes all attachments.
    /// </summary>
    Task<int> DeleteAllAttachments(SqlConnection connection, SqlTransaction? transaction, Cancel cancel = default);

    /// <summary>
    /// Deletes attachments older than <paramref name="dateTime" />.
    /// </summary>
    Task<int> CleanupItemsOlderThan(SqlConnection connection, SqlTransaction? transaction, DateTime dateTime, Cancel cancel = default);

    /// <summary>
    /// Deletes all items.
    /// </summary>
    Task<int> PurgeItems(SqlConnection connection, SqlTransaction? transaction, Cancel cancel = default);

    /// <summary>
    /// Copies an attachment to <paramref name="target" />.
    /// </summary>
    Task CopyTo(string messageId, string name, SqlConnection connection, SqlTransaction? transaction, Stream target, Cancel cancel = default);

    /// <summary>
    /// Copies an attachment to a different message.
    /// </summary>
    Task<Guid> Duplicate(string sourceMessageId, string name, SqlConnection connection, SqlTransaction? transaction, string targetMessageId, Cancel cancel = default);

    /// <summary>
    /// Copies an attachment to a different message.
    /// </summary>
    Task<Guid> Duplicate(string sourceMessageId, string name, SqlConnection connection, SqlTransaction? transaction, string targetMessageId, string targetName, Cancel cancel = default);

    /// <summary>
    /// Copies all attachments to a different message.
    /// </summary>
    Task<IReadOnlyList<(Guid, string)>> Duplicate(string sourceMessageId, SqlConnection connection, SqlTransaction? transaction, string targetMessageId, Cancel cancel = default);

    /// <summary>
    /// Reads all <see cref="AttachmentBytes" />s for an attachment.
    /// </summary>
    IAsyncEnumerable<AttachmentBytes> GetBytes(string messageId, SqlConnection connection, SqlTransaction? transaction, Cancel cancel = default);

    /// <summary>
    /// Deletes attachments for message.
    /// </summary>
    Task<int> DeleteAttachments(string messageId, SqlConnection connection, SqlTransaction? transaction, Cancel cancel = default);

    /// <summary>
    /// Reads all <see cref="AttachmentString" />s for an attachment.
    /// </summary>
    IAsyncEnumerable<AttachmentString> GetStrings(string messageId, SqlConnection connection, SqlTransaction? transaction, Encoding? encoding, Cancel cancel = default);

    /// <summary>
    /// Reads all <see cref="AttachmentStream" />s to an attachment.
    /// </summary>
    IAsyncEnumerable<AttachmentStream> GetStreams(string messageId, SqlConnection connection, SqlTransaction? transaction, Cancel cancel = default);

    /// <summary>
    /// Reads an <see cref="AttachmentBytes" /> for an attachment.
    /// </summary>
    Task<AttachmentBytes> GetBytes(string messageId, string name, SqlConnection connection, SqlTransaction? transaction, Cancel cancel = default);

    /// <summary>
    /// Reads an <see cref="AttachmentString" /> for an attachment.
    /// </summary>
    Task<AttachmentString> GetString(string messageId, string name, SqlConnection connection, SqlTransaction? transaction, Encoding? encoding, Cancel cancel = default);

    /// <summary>
    /// Reads an <see cref="AttachmentStream" /> for an attachment.
    /// </summary>
    Task<AttachmentStream> GetStream(string messageId, string name, SqlConnection connection, SqlTransaction? transaction, bool disposeConnectionOnStreamDispose, Cancel cancel = default);

    /// <summary>
    /// Reads an <see cref="AttachmentStream" /> for an attachment.
    /// </summary>
    Task<MemoryStream> GetMemoryStream(string messageId, string name, SqlConnection connection, SqlTransaction? transaction, Cancel cancel = default);

    /// <summary>
    /// Processes all attachments for <paramref name="messageId" /> by passing them to <paramref name="action" />.
    /// </summary>
    Task ProcessStreams(string messageId, SqlConnection connection, SqlTransaction? transaction, Func<AttachmentStream, Cancel, Task> action, Cancel cancel = default);

    /// <summary>
    /// Processes an attachment by passing it to <paramref name="action" />.
    /// </summary>
    Task ProcessStream(string messageId, string name, SqlConnection connection, SqlTransaction? transaction, Func<AttachmentStream, Cancel, Task> action, Cancel cancel = default);

    /// <summary>
    /// Processes all attachments for <paramref name="messageId" /> by passing them to <paramref name="action" />.
    /// </summary>
    Task ProcessByteArrays(string messageId, SqlConnection connection, SqlTransaction? transaction, Func<AttachmentBytes, Cancel, Task> action, Cancel cancel = default);

    /// <summary>
    /// Processes an attachment by passing it to <paramref name="action" />.
    /// </summary>
    Task ProcessByteArray(string messageId, string name, SqlConnection connection, SqlTransaction? transaction, Func<AttachmentBytes, Cancel, Task> action, Cancel cancel = default);
}