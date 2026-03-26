using System.IO.Pipelines;
using Microsoft.Data.SqlClient;
using NServiceBus.Attachments.Sql;
using NServiceBus.Pipeline;

class SendBehavior :
    Behavior<IOutgoingLogicalMessageContext>
{
    Func<Cancel, Task<SqlConnection>> connectionFactory;
    IPersister persister;
    GetTimeToKeep endpointTimeToKeep;

    public SendBehavior(Func<Cancel, Task<SqlConnection>> connectionFactory, IPersister persister, GetTimeToKeep timeToKeep)
    {
        this.connectionFactory = connectionFactory;
        this.persister = persister;
        endpointTimeToKeep = timeToKeep;
    }

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
        var attachments = new Dictionary<Guid, string>();
        foreach (var (name, value) in outgoingAttachments.Inner)
        {
            var guid = await ProcessAttachment(timeToBeReceived, connection, transaction, context.MessageId, value, name);
            attachments.Add(guid, name);
        }

        foreach (var dynamic in outgoingAttachments.Dynamic)
        {
            await dynamic(async (name, stream, keep, cleanup, metadata) =>
            {
                var capturedStream = stream;
                var outgoing = new Outgoing
                {
                    Cleanup = cleanup,
                    StreamWriter = async target => await capturedStream.CopyToAsync(target),
                    Metadata = metadata,
                    TimeToKeep = keep,
                };
                var guid = await ProcessAttachment(timeToBeReceived, connection, transaction, context.MessageId, outgoing, name);
                attachments.Add(guid, name);
            });
        }

        if (outgoingAttachments.DuplicateIncomingAttachments || outgoingAttachments.Duplicates.Count != 0)
        {
            var incomingMessageId = context.IncomingMessageId();
            if (outgoingAttachments.DuplicateIncomingAttachments)
            {
                var names = await persister.Duplicate(incomingMessageId, connection, transaction, context.MessageId, context.CancellationToken);
                foreach (var (id, name) in names)
                {
                    attachments.Add(id, name);
                }
            }

            foreach (var duplicate in outgoingAttachments.Duplicates)
            {
                var guid = await persister.Duplicate(incomingMessageId, duplicate.From, connection, transaction, context.MessageId, duplicate.To, context.CancellationToken);
                attachments.Add(guid, duplicate.To);
            }
        }

        Guard.AgainstDuplicateNames(attachments.Values);

        context.Headers.Add("Attachments", string.Join(", ", attachments.Select(_ => $"{_.Key}: {_.Value}")));
    }

    async Task<Guid> ProcessWriter(SqlConnection connection, SqlTransaction? transaction, string messageId, string name, DateTime expiry, Func<Stream, Task> writer, IReadOnlyDictionary<string, string>? metadata)
    {
        var pipe = new Pipe();
        var writerTask = Task.Run(async () =>
        {
            try
            {
                await writer(pipe.Writer.AsStream());
            }
            finally
            {
                await pipe.Writer.CompleteAsync();
            }
        });

        var readerStream = pipe.Reader.AsStream();
        await using (readerStream)
        {
            var guid = await persister.SaveStream(connection, transaction, messageId, name, expiry, readerStream, metadata);
            await writerTask;
            return guid;
        }
    }

    async Task<Guid> ProcessAttachment(TimeSpan? timeToBeReceived, SqlConnection connection, SqlTransaction? transaction, string messageId, Outgoing outgoing, string name)
    {
        var outgoingStreamTimeToKeep = outgoing.TimeToKeep ?? endpointTimeToKeep;
        var timeToKeep = outgoingStreamTimeToKeep(timeToBeReceived);
        var expiry = DateTime.UtcNow.Add(timeToKeep);
        try
        {
            return await Process(connection, transaction, messageId, outgoing, name, expiry);
        }
        finally
        {
            outgoing.Cleanup?.Invoke();
        }
    }

    async Task<Guid> Process(SqlConnection connection, SqlTransaction? transaction, string messageId, Outgoing outgoing, string name, DateTime expiry)
    {
        var metadata = outgoing.Metadata;
        if (outgoing.StreamWriter is not null)
        {
            return await ProcessWriter(connection, transaction, messageId, name, expiry, outgoing.StreamWriter, metadata);
        }

        if (outgoing.AsyncBytesFactory is not null)
        {
            var bytes = await outgoing.AsyncBytesFactory();
            return await persister.SaveBytes(connection, transaction, messageId, name, expiry, bytes, metadata);
        }

        if (outgoing.BytesFactory is not null)
        {
            return await persister.SaveBytes(connection, transaction, messageId, name, expiry, outgoing.BytesFactory(), metadata);
        }

        if (outgoing.BytesInstance is not null)
        {
            return await persister.SaveBytes(connection, transaction, messageId, name, expiry, outgoing.BytesInstance, metadata);
        }

        if (outgoing.StringInstance is not null)
        {
            return await persister.SaveString(connection, transaction, messageId, name, expiry, outgoing.StringInstance, outgoing.Encoding, metadata);
        }

        throw new("No matching way to handle outgoing.");
    }
}