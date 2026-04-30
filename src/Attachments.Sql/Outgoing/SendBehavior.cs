using Microsoft.Data.SqlClient;
using NServiceBus.Attachments.Sql;
using NServiceBus.Pipeline;

class SendBehavior(Func<Cancel, Task<SqlConnection>> connectionFactory, IPersister persister, GetTimeToKeep endpointTimeToKeep)
    :
        Behavior<IOutgoingLogicalMessageContext>
{
    public override async Task Invoke(IOutgoingLogicalMessageContext context, Func<Task> next)
    {
        await ProcessStreams(context);
        await next();
    }

    async Task ProcessStreams(IOutgoingLogicalMessageContext context)
    {
        var extensions = context.Extensions;
        if (!extensions.TryGet<IOutgoingAttachments>(out var attachments))
        {
            return;
        }

        var outgoingAttachments = (OutgoingAttachments) attachments;
        if (!outgoingAttachments.HasPendingAttachments)
        {
            return;
        }

        var timeToBeReceived = extensions.GetTimeToBeReceivedFromConstraint();

        if (context.Extensions.TryGet<SqlAttachmentState>(out var state))
        {
            if (state.Transaction is not null)
            {
                await using var connectionFromState = await state.GetConnection(context.CancellationToken);
                connectionFromState.EnlistTransaction(state.Transaction);
                await ProcessOutgoing(timeToBeReceived, connectionFromState, null, context, outgoingAttachments);
                return;
            }

            if (state.SqlTransaction is not null)
            {
                await ProcessOutgoing(timeToBeReceived, state.SqlTransaction.Connection!, state.SqlTransaction, context, outgoingAttachments);
                return;
            }

            if (state.SqlConnection is not null)
            {
                await ProcessOutgoing(timeToBeReceived, state.SqlConnection, null, context, outgoingAttachments);
                return;
            }

            await using var connection = await state.GetConnection(context.CancellationToken);
            await ProcessOutgoing(timeToBeReceived, connection, null, context, outgoingAttachments);
            return;
        }

        await using var connectionFromFactory = await connectionFactory(context.CancellationToken);
        //TODO: should this be done ?
        if (context.TryReadTransaction(out var transaction))
        {
            connectionFromFactory.EnlistTransaction(transaction);
        }

        await using var dbTransaction = connectionFromFactory.BeginTransaction();
        await ProcessOutgoing(timeToBeReceived, connectionFromFactory, dbTransaction, context, outgoingAttachments);
        dbTransaction.Commit();
    }

    async Task ProcessOutgoing(TimeSpan? timeToBeReceived, SqlConnection connection, SqlTransaction? transaction, IOutgoingLogicalMessageContext context, OutgoingAttachments outgoingAttachments)
    {
        var cancel = context.CancellationToken;
        var attachments = new Dictionary<Guid, string>();
        string? incomingMessageId = null;
        string GetIncomingMessageId() => incomingMessageId ??= context.IncomingMessageId();

        foreach (var (name, value) in outgoingAttachments.Inner)
        {
            var guid = await ProcessAttachment(timeToBeReceived, connection, transaction, context.MessageId, GetIncomingMessageId, value, name, cancel);
            attachments.Add(guid, name);
        }

        foreach (var dynamic in outgoingAttachments.Dynamic)
        {
            await dynamic(async (name, stream, keep, cleanup, metadata) =>
            {
                var outgoing = new Outgoing
                {
                    Cleanup = cleanup,
                    StreamWriter = stream.CopyToAsync,
                    Metadata = metadata,
                    TimeToKeep = keep,
                };
                var guid = await ProcessAttachment(timeToBeReceived, connection, transaction, context.MessageId, GetIncomingMessageId, outgoing, name, cancel);
                attachments.Add(guid, name);
            });
        }

        if (outgoingAttachments.DuplicateIncomingAttachments || outgoingAttachments.Duplicates.Count != 0)
        {
            if (outgoingAttachments.DuplicateIncomingAttachments)
            {
                var names = await persister.Duplicate(GetIncomingMessageId(), connection, transaction, context.MessageId, context.CancellationToken);
                foreach (var (id, name) in names)
                {
                    attachments.Add(id, name);
                }
            }

            foreach (var duplicate in outgoingAttachments.Duplicates)
            {
                var guid = await persister.Duplicate(GetIncomingMessageId(), duplicate.From, connection, transaction, context.MessageId, duplicate.To, context.CancellationToken);
                attachments.Add(guid, duplicate.To);
            }
        }

        Guard.AgainstDuplicateNames(attachments.Values);

        context.Headers.Add("Attachments", string.Join(", ", attachments.Select(_ => $"{_.Key}: {_.Value}")));
    }

    async Task<Guid> ProcessWriter(SqlConnection connection, SqlTransaction? transaction, string messageId, string name, DateTime expiry, Func<Stream, Task> writer, IReadOnlyDictionary<string, string>? metadata, Cancel cancel)
    {
        var (writerTask, readerStream) = PipeHelper.StartWriter(writer, cancel);
        await using (readerStream)
        {
            var guid = await persister.SaveStream(connection, transaction, messageId, name, expiry, readerStream, metadata, cancel);
            await writerTask;
            return guid;
        }
    }

    async Task<Guid> ProcessAttachment(TimeSpan? timeToBeReceived, SqlConnection connection, SqlTransaction? transaction, string messageId, Func<string> getIncomingMessageId, Outgoing outgoing, string name, Cancel cancel)
    {
        var outgoingStreamTimeToKeep = outgoing.TimeToKeep ?? endpointTimeToKeep;
        var timeToKeep = outgoingStreamTimeToKeep(timeToBeReceived);
        var expiry = DateTime.UtcNow.Add(timeToKeep);
        try
        {
            return await Process(connection, transaction, messageId, getIncomingMessageId, outgoing, name, expiry, cancel);
        }
        finally
        {
            outgoing.Cleanup?.Invoke();
        }
    }

    async Task<Guid> Process(SqlConnection connection, SqlTransaction? transaction, string messageId, Func<string> getIncomingMessageId, Outgoing outgoing, string name, DateTime expiry, Cancel cancel)
    {
        var metadata = outgoing.Metadata;
        if (outgoing.IsPreSaved)
        {
            return outgoing.PreSavedGuid ?? throw new("Pre-saved Sql attachment is missing the row guid.");
        }

        if (outgoing.HasIncomingTransform)
        {
            return await ProcessIncomingTransform(connection, transaction, messageId, getIncomingMessageId(), outgoing, name, expiry, cancel);
        }

        if (outgoing.StreamWriter is not null)
        {
            return await ProcessWriter(connection, transaction, messageId, name, expiry, outgoing.StreamWriter, metadata, cancel);
        }

        if (outgoing.AsyncBytesFactory is not null)
        {
            var bytes = await outgoing.AsyncBytesFactory();
            return await persister.SaveStream(connection, transaction, messageId, name, expiry, new MemoryStream(bytes), metadata, cancel);
        }

        if (outgoing.BytesFactory is not null)
        {
            return await persister.SaveStream(connection, transaction, messageId, name, expiry, new MemoryStream(outgoing.BytesFactory()), metadata, cancel);
        }

        if (outgoing.BytesInstance is not null)
        {
            return await persister.SaveStream(connection, transaction, messageId, name, expiry, new MemoryStream(outgoing.BytesInstance), metadata, cancel);
        }

        if (outgoing.StringInstance is not null)
        {
            return await persister.SaveString(connection, transaction, messageId, name, expiry, outgoing.StringInstance, outgoing.Encoding, metadata, cancel);
        }

        throw new("No matching way to handle outgoing.");
    }

    Task<Guid> ProcessIncomingTransform(SqlConnection sendConnection, SqlTransaction? sendTransaction, string toMessageId, string fromMessageId, Outgoing outgoing, string name, DateTime expiry, Cancel cancel)
    {
        var transform = outgoing.IncomingTransform!;
        var fromName = outgoing.IncomingFromName!;
        var bufferSource = outgoing.BufferSource;
        var bufferSink = outgoing.BufferSink;

        Func<Stream, Task> writer = async sink =>
        {
            await using var readConnection = await connectionFactory(cancel);
            await persister.ProcessStream(
                fromMessageId,
                fromName,
                readConnection,
                null,
                async (inStream, c) =>
                {
                    if (bufferSource)
                    {
                        using var bufferedSource = new MemoryStream();
                        await inStream.CopyToAsync(bufferedSource, c);
                        bufferedSource.Position = 0;
                        await RunTransform(transform, bufferedSource, sink, bufferSink, c);
                    }
                    else
                    {
                        await RunTransform(transform, inStream, sink, bufferSink, c);
                    }
                },
                cancel);
        };

        return ProcessWriter(sendConnection, sendTransaction, toMessageId, name, expiry, writer, outgoing.Metadata, cancel);
    }

    static async Task RunTransform(Func<Stream, Stream, Cancel, Task> transform, Stream source, Stream pipeSink, bool bufferSink, Cancel cancel)
    {
        if (bufferSink)
        {
            using var bufferedSink = new MemoryStream();
            await transform(source, bufferedSink, cancel);
            bufferedSink.Position = 0;
            await bufferedSink.CopyToAsync(pipeSink, cancel);
        }
        else
        {
            await transform(source, pipeSink, cancel);
        }
    }
}
