using System.IO.Pipelines;
using Microsoft.Data.SqlClient;
using NServiceBus.Attachments.Sql;
using NServiceBus.Extensibility;

namespace NServiceBus;

public static partial class SqlAttachmentsMessageContextExtensions
{
    /// <summary>
    /// Open a writable <see cref="Stream"/> for an outgoing attachment that is persisted immediately
    /// (during handler execution) rather than via the deferred outgoing pipeline. The handler writes
    /// to the returned stream and disposes it; on dispose the data is committed to SQL using the
    /// receive context's connection/transaction and the attachment is registered on
    /// <paramref name="options"/> so the outgoing message carries it in the Attachments header. Use
    /// when the handler needs the converter's output values (for example a "truncated" flag or
    /// encoding metadata) before composing the outgoing message — a case <c>AddFromIncoming</c>
    /// cannot serve.
    /// </summary>
    public static Task<Stream> OpenOutgoingAttachment(
        this HandlerContext context,
        SendOptions options,
        string name,
        GetTimeToKeep? timeToKeep = null,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        OpenForOptions(context, options, name, timeToKeep, metadata);

    /// <summary>
    /// See <see cref="OpenOutgoingAttachment(HandlerContext,SendOptions,string,GetTimeToKeep?,IReadOnlyDictionary{string,string}?)"/>.
    /// </summary>
    public static Task<Stream> OpenOutgoingAttachment(
        this HandlerContext context,
        ReplyOptions options,
        string name,
        GetTimeToKeep? timeToKeep = null,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        OpenForOptions(context, options, name, timeToKeep, metadata);

    /// <summary>
    /// See <see cref="OpenOutgoingAttachment(HandlerContext,SendOptions,string,GetTimeToKeep?,IReadOnlyDictionary{string,string}?)"/>.
    /// </summary>
    public static Task<Stream> OpenOutgoingAttachment(
        this HandlerContext context,
        PublishOptions options,
        string name,
        GetTimeToKeep? timeToKeep = null,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        OpenForOptions(context, options, name, timeToKeep, metadata);

    static async Task<Stream> OpenForOptions(
        HandlerContext context,
        ExtendableOptions options,
        string name,
        GetTimeToKeep? timeToKeep,
        IReadOnlyDictionary<string, string>? metadata)
    {
        Guard.AgainstNullOrEmpty(name);

        if (!context.Extensions.TryGet<SqlAttachmentState>(out var state))
        {
            throw new("OpenOutgoingAttachment used when attachments not enabled. Call EndpointConfiguration.EnableAttachments() first.");
        }

        var cancel = context.CancellationToken;
        var messageId = options.GetMessageId() ?? Guid.NewGuid().ToString();
        options.SetMessageId(messageId);

        var keep = (timeToKeep ?? TimeToKeep.Default)(null);
        var expiry = DateTime.UtcNow.Add(keep);

        var (connection, transaction, ownsConnection) = await ResolveConnection(state, cancel);

        var pipe = new Pipe();
        var saveTask = state.Persister.SaveStream(
            connection,
            transaction,
            messageId,
            name,
            expiry,
            pipe.Reader.AsStream(),
            metadata,
            cancel);

        var outgoing = (OutgoingAttachments) GetOutgoingAttachments(options);
        var pipeWriterStream = pipe.Writer.AsStream();
        var writerStream = new PositionTrackingStream(pipeWriterStream);

        return new ImmediateAttachmentStream(
            inner: writerStream,
            commitAsync: async () =>
            {
                // Disposing the pipe writer stream completes the writer; the SaveStream's
                // SqlCommand then finishes reading the binary parameter and the INSERT runs.
                await pipeWriterStream.DisposeAsync();
                try
                {
                    return await saveTask;
                }
                finally
                {
                    if (ownsConnection)
                    {
                        await connection.DisposeAsync();
                    }
                }
            },
            onCommitted: guid => outgoing.AddPreSaved(name, savedGuid: guid, metadata));
    }

    static async Task<(SqlConnection connection, SqlTransaction? transaction, bool ownsConnection)> ResolveConnection(SqlAttachmentState state, Cancel cancel)
    {
        if (state.Transaction is not null)
        {
            var connection = await state.GetConnection(cancel);
            connection.EnlistTransaction(state.Transaction);
            return (connection, null, ownsConnection: true);
        }

        if (state.SqlTransaction is not null)
        {
            return (state.SqlTransaction.Connection!, state.SqlTransaction, ownsConnection: false);
        }

        if (state.SqlConnection is not null)
        {
            return (state.SqlConnection, null, ownsConnection: false);
        }

        var freshConnection = await state.GetConnection(cancel);
        return (freshConnection, null, ownsConnection: true);
    }

    static IOutgoingAttachments GetOutgoingAttachments(ExtendableOptions options)
    {
        var contextBag = options.GetExtensions();
        if (contextBag.TryGet<IOutgoingAttachments>(out var attachments))
        {
            return attachments;
        }

        attachments = new OutgoingAttachments();
        contextBag.Set(attachments);
        return attachments;
    }
}
