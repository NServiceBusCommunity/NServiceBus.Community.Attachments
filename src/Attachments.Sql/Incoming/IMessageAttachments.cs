namespace NServiceBus.Attachments.Sql;

/// <summary>
/// Provides access to read attachments.
/// </summary>
public partial interface IMessageAttachments
{
    /// <summary>
    /// Get a <see cref="Stream" />, for the current message, the attachment with the default name of <see cref="string.Empty" />.
    /// </summary>
    Task<AttachmentStream> GetStream(Cancel cancel = default);

    /// <summary>
    /// Get a <see cref="Stream" />, for the current message, the attachment of <paramref name="name" />.
    /// </summary>
    Task<AttachmentStream> GetStream(string name, Cancel cancel = default);

    /// <summary>
    /// Get a <see cref="Stream" />, for the message with <paramref name="messageId" />, the attachment with the default name of <see cref="string.Empty" />.
    /// </summary>
    Task<AttachmentStream> GetStreamForMessage(string messageId, Cancel cancel = default);

    /// <summary>
    /// Get a <see cref="Stream" />, for the message with <paramref name="messageId" />, the attachment of <paramref name="name" />.
    /// </summary>
    Task<AttachmentStream> GetStreamForMessage(string messageId, string name, Cancel cancel = default);

    /// <summary>
    /// Process with the delegate <paramref name="action"/>, for the current message, the attachment of <paramref name="name"/>.
    /// </summary>
    Task ProcessByteArray(string name, Func<AttachmentBytes, Cancel, Task> action, Cancel cancel = default);

    /// <summary>
    /// Process with the delegate <paramref name="action"/>, the attachment with the default name of <see cref="string.Empty"/>.
    /// </summary>
    Task ProcessByteArray(Func<AttachmentBytes, Cancel, Task> action, Cancel cancel = default);

    /// <summary>
    /// Process with the delegate <paramref name="action"/>, all attachments for the current message.
    /// </summary>
    Task ProcessByteArrays(Func<AttachmentBytes, Cancel, Task> action, Cancel cancel = default);

    /// <summary>
    /// Process with the delegate <paramref name="action"/>, for the message with <paramref name="messageId"/>, the attachment of <paramref name="name"/>.
    /// </summary>
    Task ProcessByteArrayForMessage(string messageId, string name, Func<AttachmentBytes, Cancel, Task> action, Cancel cancel = default);

    /// <summary>
    /// Process with the delegate <paramref name="action"/>, for the message with <paramref name="messageId"/>, the attachment with the default name of <see cref="string.Empty"/>.
    /// </summary>
    Task ProcessByteArrayForMessage(string messageId, Func<AttachmentBytes, Cancel, Task> action, Cancel cancel = default);

    /// <summary>
    /// Process with the delegate <paramref name="action"/>, all attachments for the message with <paramref name="messageId"/>.
    /// </summary>
    Task ProcessByteArraysForMessage(string messageId, Func<AttachmentBytes, Cancel, Task> action, Cancel cancel = default);

    /// <summary>
    /// Transfer ownership of the attachment with the default name of <see cref="string.Empty"/>, for the current message, to <paramref name="saga"/>.
    /// The attachment row is updated in place, so the attachment data is not copied.
    /// Once transferred the attachment is no longer removed by early cleanup of the current message.
    /// It is removed by <see cref="DeleteForSaga"/> or, once it expires, by the cleanup task.
    /// <paramref name="newName"/> is the name to give the attachment in <paramref name="saga"/>. When null the existing name is kept.
    /// <paramref name="timeToKeep"/> is how long to keep the attachment, from now. When null the existing expiry is kept.
    /// </summary>
    Task TransferToSaga(IContainSagaData saga, string? newName = null, TimeSpan? timeToKeep = null, Cancel cancel = default);

    /// <summary>
    /// Transfer ownership of the attachment of <paramref name="name"/>, for the current message, to <paramref name="saga"/>.
    /// The attachment row is updated in place, so the attachment data is not copied.
    /// Once transferred the attachment is no longer removed by early cleanup of the current message.
    /// It is removed by <see cref="DeleteForSaga"/> or, once it expires, by the cleanup task.
    /// <paramref name="newName"/> is the name to give the attachment in <paramref name="saga"/>. When null the existing name is kept.
    /// <paramref name="timeToKeep"/> is how long to keep the attachment, from now. When null the existing expiry is kept.
    /// </summary>
    Task TransferToSaga(string name, IContainSagaData saga, string? newName = null, TimeSpan? timeToKeep = null, Cancel cancel = default);

    /// <summary>
    /// Get a <see cref="byte"/> array, for <paramref name="saga"/>, the attachment of <paramref name="name"/>.
    /// Runs on the connection of the current receive, so it sees attachments transferred by the current handler.
    /// </summary>
    Task<AttachmentBytes> GetBytesForSaga(IContainSagaData saga, string name, Cancel cancel = default);

    /// <summary>
    /// Get a <see cref="MemoryStream"/>, for <paramref name="saga"/>, the attachment of <paramref name="name"/>.
    /// Runs on the connection of the current receive, so it sees attachments transferred by the current handler.
    /// </summary>
    Task<MemoryStream> GetMemoryStreamForSaga(IContainSagaData saga, string name, Cancel cancel = default);

    /// <summary>
    /// Get a <see cref="string"/>, for <paramref name="saga"/>, the attachment of <paramref name="name"/>.
    /// Runs on the connection of the current receive, so it sees attachments transferred by the current handler.
    /// </summary>
    Task<AttachmentString> GetStringForSaga(IContainSagaData saga, string name, Encoding? encoding = null, Cancel cancel = default);

    /// <summary>
    /// Delete all attachments owned by <paramref name="saga"/>.
    /// Runs on the connection of the current receive, so the delete is rolled back if the handler fails.
    /// </summary>
    /// <returns>The number of attachments deleted.</returns>
    Task<int> DeleteForSaga(IContainSagaData saga, Cancel cancel = default);
}