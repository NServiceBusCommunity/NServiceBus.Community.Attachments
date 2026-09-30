namespace NServiceBus.Attachments.Sql.Testing;

public partial class StubMessageAttachments
{
    /// <inheritdoc />
    public virtual Task<AttachmentStream> GetStream(Cancel cancel = default) =>
        GetStream("default", cancel);

    /// <inheritdoc />
    public virtual Task<AttachmentStream> GetStream(string name, Cancel cancel = default)
    {
        var attachment = GetCurrentMessageAttachment(name);
        var attachmentStream = attachment.ToAttachmentStream();
        return Task.FromResult(attachmentStream);
    }

    /// <inheritdoc />
    public virtual Task<AttachmentStream> GetStreamForMessage(string messageId, Cancel cancel = default) =>
        GetStreamForMessage(messageId, "default", cancel);

    /// <inheritdoc />
    public virtual Task<AttachmentStream> GetStreamForMessage(string messageId, string name, Cancel cancel = default)
    {
        var attachment = GetAttachmentForMessage(messageId, name);
        return Task.FromResult(attachment.ToAttachmentStream());
    }
    /// <inheritdoc />
    public virtual Task ProcessByteArray(string name, Func<AttachmentBytes, Cancel, Task> action, Cancel cancel = default) =>
        InnerProcessByteArray(name, action, cancel);

    /// <inheritdoc />
    public virtual Task ProcessByteArray(Func<AttachmentBytes, Cancel, Task> action, Cancel cancel = default) =>
        ProcessByteArray("default", action, cancel);

    /// <inheritdoc />
    public virtual async Task ProcessByteArrays(Func<AttachmentBytes, Cancel, Task> action, Cancel cancel = default)
    {
        foreach (var pair in currentAttachments)
        {
            await using var attachmentStream = pair.Value.ToAttachmentStream();
            await action(pair.Value.ToAttachmentBytes(), cancel);
        }
    }
    /// <inheritdoc />
    public virtual Task ProcessByteArrayForMessage(string messageId, string name, Func<AttachmentBytes, Cancel, Task> action, Cancel cancel = default)
    {
        var attachment = GetAttachmentForMessage(messageId, name);
        return action(attachment.ToAttachmentBytes(), cancel);
    }

    /// <inheritdoc />
    public virtual Task ProcessByteArrayForMessage(string messageId, Func<AttachmentBytes, Cancel, Task> action, Cancel cancel = default) =>
        ProcessByteArrayForMessage(messageId, "default", action, cancel);

    /// <inheritdoc />
    public virtual async Task ProcessByteArraysForMessage(string messageId, Func<AttachmentBytes, Cancel, Task> action, Cancel cancel = default)
    {
        foreach (var pair in GetAttachmentsForMessage(messageId))
        {
            await action(pair.Value.ToAttachmentBytes(), cancel);
        }
    }

    /// <inheritdoc />
    public virtual Task TransferToSaga(IContainSagaData saga, string? newName = null, TimeSpan? timeToKeep = null, Cancel cancel = default) =>
        TransferToSaga("default", saga, newName, timeToKeep, cancel);

    /// <inheritdoc />
    public virtual Task TransferToSaga(string name, IContainSagaData saga, string? newName = null, TimeSpan? timeToKeep = null, Cancel cancel = default)
    {
        var attachment = GetCurrentMessageAttachment(name);
        var owner = SagaAttachmentOwner.Key(saga);
        if (!attachments.TryGetValue(owner, out var attachmentsForSaga))
        {
            attachments[owner] = attachmentsForSaga = new(StringComparer.OrdinalIgnoreCase);
        }

        var targetName = newName ?? attachment.Name;
        if (attachmentsForSaga.ContainsKey(targetName))
        {
            throw new($"Could not transfer attachment. An attachment named '{targetName}' already exists for '{owner}'. Name:{name}");
        }

        currentAttachments.Remove(name);
        attachment.Name = targetName;
        var expiry = SagaAttachmentOwner.Expiry(timeToKeep);
        if (expiry is not null)
        {
            attachment.Expiry = expiry.Value;
        }

        attachmentsForSaga.Add(targetName, attachment);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task<AttachmentBytes> GetBytesForSaga(IContainSagaData saga, string name, Cancel cancel = default) =>
        GetBytesForMessage(SagaAttachmentOwner.Key(saga), name, cancel);

    /// <inheritdoc />
    public virtual Task<MemoryStream> GetMemoryStreamForSaga(IContainSagaData saga, string name, Cancel cancel = default) =>
        GetMemoryStreamForMessage(SagaAttachmentOwner.Key(saga), name, cancel);

    /// <inheritdoc />
    public virtual Task<AttachmentString> GetStringForSaga(IContainSagaData saga, string name, Encoding? encoding = null, Cancel cancel = default) =>
        GetStringForMessage(SagaAttachmentOwner.Key(saga), name, encoding, cancel);

    /// <inheritdoc />
    public virtual Task<int> DeleteForSaga(IContainSagaData saga, Cancel cancel = default)
    {
        var owner = SagaAttachmentOwner.Key(saga);
        if (attachments.Remove(owner, out var attachmentsForSaga))
        {
            return Task.FromResult(attachmentsForSaga.Count);
        }

        return Task.FromResult(0);
    }
}