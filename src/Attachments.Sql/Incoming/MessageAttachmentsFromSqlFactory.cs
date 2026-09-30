using Microsoft.Data.SqlClient;
using NServiceBus.Attachments.Sql;
// ReSharper disable ParameterHidesPrimaryConstructorParameter

class MessageAttachmentsFromSqlFactory(SqlAttachmentState state, string messageId) :
    IMessageAttachments
{
    Func<Cancel, Task<SqlConnection>> connectionFactory = state.GetConnection;
    IPersister persister = state.Persister;

    public Task TransferToSaga(IContainSagaData saga, string? newName = null, TimeSpan? timeToKeep = null, Cancel cancel = default) =>
        TransferToSaga("default", saga, newName, timeToKeep, cancel);

    // Runs on the receive transaction so the transfer only commits if the handler succeeds.
    // Early cleanup then deletes by the incoming message id, which no longer matches the row.
    public Task TransferToSaga(string name, IContainSagaData saga, string? newName = null, TimeSpan? timeToKeep = null, Cancel cancel = default)
    {
        var owner = SagaAttachmentOwner.Key(saga);
        var expiry = SagaAttachmentOwner.Expiry(timeToKeep);
        return state.Execute(
            (connection, transaction) => persister.Transfer(messageId, name, connection, transaction, owner, newName, expiry, cancel),
            cancel);
    }

    // The ForSaga reads run on the receive transaction, rather than a fresh connection, so they see
    // attachments transferred earlier in the same handler. They are all buffered so that no reader is
    // left open on the shared connection.
    public Task<AttachmentBytes> GetBytesForSaga(IContainSagaData saga, string name, Cancel cancel = default)
    {
        var owner = SagaAttachmentOwner.Key(saga);
        return state.Execute(
            (connection, transaction) => persister.GetBytes(owner, name, connection, transaction, cancel),
            cancel);
    }

    public Task<MemoryStream> GetMemoryStreamForSaga(IContainSagaData saga, string name, Cancel cancel = default)
    {
        var owner = SagaAttachmentOwner.Key(saga);
        return state.Execute(
            (connection, transaction) => persister.GetMemoryStream(owner, name, connection, transaction, cancel),
            cancel);
    }

    public Task<AttachmentString> GetStringForSaga(IContainSagaData saga, string name, Encoding? encoding = null, Cancel cancel = default)
    {
        var owner = SagaAttachmentOwner.Key(saga);
        return state.Execute(
            (connection, transaction) => persister.GetString(owner, name, connection, transaction, encoding, cancel),
            cancel);
    }

    public Task<int> DeleteForSaga(IContainSagaData saga, Cancel cancel = default)
    {
        var owner = SagaAttachmentOwner.Key(saga);
        return state.Execute(
            (connection, transaction) => persister.DeleteAttachments(owner, connection, transaction, cancel),
            cancel);
    }

    public async Task CopyTo(Stream target, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        await persister.CopyTo(messageId, "default", connection, null, target, cancel);
    }

    public async Task CopyTo(string name, Stream target, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        await persister.CopyTo(messageId, name, connection, null, target, cancel);
    }

    public async Task ProcessStream(Func<AttachmentStream, Cancel, Task> action, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        await persister.ProcessStream(messageId, "default", connection, null, action, cancel);
    }

    public async Task ProcessByteArray(Func<AttachmentBytes, Cancel, Task> action, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        await persister.ProcessByteArray(messageId, "default", connection, null, action, cancel);
    }

    public async Task ProcessStream(string name, Func<AttachmentStream, Cancel, Task> action, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        await persister.ProcessStream(messageId, name, connection, null, action, cancel);
    }

    public async Task ProcessByteArray(string name, Func<AttachmentBytes, Cancel, Task> action, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        await persister.ProcessByteArray(messageId, name, connection, null, action, cancel);
    }

    public async Task ProcessStreams(Func<AttachmentStream, Cancel, Task> action, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        await persister.ProcessStreams(messageId, connection, null, action, cancel);
    }

    public async Task ProcessByteArrays(Func<AttachmentBytes, Cancel, Task> action, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        await persister.ProcessByteArrays(messageId, connection, null, action, cancel);
    }

    public async Task<AttachmentBytes> GetBytes(Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        return await persister.GetBytes(messageId, "default", connection, null, cancel);
    }

    public async Task<MemoryStream> GetMemoryStream(Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        return await persister.GetMemoryStream(messageId, "default", connection, null, cancel);
    }

    public async Task<AttachmentString> GetString(Encoding? encoding, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        return await persister.GetString(messageId, "default", connection, null, encoding, cancel);
    }

    public async Task<AttachmentBytes> GetBytes(string name, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        return await persister.GetBytes(messageId, name, connection, null, cancel);
    }

    public async Task<MemoryStream> GetMemoryStream(string name, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        return await persister.GetMemoryStream(messageId, name, connection, null, cancel);
    }

    public async Task<AttachmentString> GetString(string name, Encoding? encoding, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        return await persister.GetString(messageId, name, connection, null, encoding, cancel);
    }

    public async Task<AttachmentStream> GetStream(Cancel cancel = default)
    {
        var connection = await connectionFactory(cancel);
        return await persister.GetStream(messageId, "default", connection, null, true, cancel);
    }

    public async Task<AttachmentStream> GetStream(string name, Cancel cancel = default)
    {
        var connection = await connectionFactory(cancel);
        return await persister.GetStream(messageId, name, connection, null, true, cancel);
    }

    public async Task CopyToForMessage(string messageId, Stream target, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        await persister.CopyTo(messageId, "default", connection, null, target, cancel);
    }

    public async Task CopyToForMessage(string messageId, string name, Stream target, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        await persister.CopyTo(messageId, name, connection, null, target, cancel);
    }

    public async Task ProcessStreamForMessage(string messageId, Func<AttachmentStream, Cancel, Task> action, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        await persister.ProcessStream(messageId, "default", connection, null, action, cancel);
    }

    public async Task ProcessByteArrayForMessage(string messageId, Func<AttachmentBytes, Cancel, Task> action, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        await persister.ProcessByteArray(messageId, "default", connection, null, action, cancel);
    }

    public async Task ProcessStreamForMessage(string messageId, string name, Func<AttachmentStream, Cancel, Task> action, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        await persister.ProcessStream(messageId, name, connection, null, action, cancel);
    }

    public async Task ProcessByteArrayForMessage(string messageId, string name, Func<AttachmentBytes, Cancel, Task> action, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        await persister.ProcessByteArray(messageId, name, connection, null, action, cancel);
    }

    public async Task ProcessStreamsForMessage(string messageId, Func<AttachmentStream, Cancel, Task> action, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        await persister.ProcessStreams(messageId, connection, null, action, cancel);
    }

    public async Task ProcessByteArraysForMessage(string messageId, Func<AttachmentBytes, Cancel, Task> action, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        await persister.ProcessByteArrays(messageId, connection, null, action, cancel);
    }

    public async Task<AttachmentBytes> GetBytesForMessage(string messageId, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        return await persister.GetBytes(messageId, "default", connection, null, cancel);
    }

    public async Task<MemoryStream> GetMemoryStreamForMessage(string messageId, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        return await persister.GetMemoryStream(messageId, "default", connection, null, cancel);
    }

    public async Task<AttachmentBytes> GetBytesForMessage(string messageId, string name, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        return await persister.GetBytes(messageId, name, connection, null, cancel);
    }

    public async Task<MemoryStream> GetMemoryStreamForMessage(string messageId, string name, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        return await persister.GetMemoryStream(messageId, name, connection, null, cancel);
    }

    public async Task<AttachmentString> GetStringForMessage(string messageId, Encoding? encoding, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        return await persister.GetString(messageId, "default", connection, null, encoding, cancel);
    }

    public async Task<AttachmentString> GetStringForMessage(string messageId, string name, Encoding? encoding, Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        return await persister.GetString(messageId, name, connection, null, encoding, cancel);
    }

    public async Task<AttachmentStream> GetStreamForMessage(string messageId, Cancel cancel = default)
    {
        var connection = await connectionFactory(cancel);
        return await persister.GetStream(messageId, "default", connection, null, true, cancel);
    }

    public async Task<AttachmentStream> GetStreamForMessage(string messageId, string name, Cancel cancel = default)
    {
        var connection = await connectionFactory(cancel);
        return await persister.GetStream(messageId, name, connection, null, true, cancel);
    }

    public async IAsyncEnumerable<AttachmentInfo> GetMetadata([EnumeratorCancellation] Cancel cancel = default)
    {
        await using var connection = await connectionFactory(cancel);
        await foreach (var info in persister.ReadAllMessageInfo(connection, null, messageId, cancel))
        {
            yield return info;
        }
    }
}