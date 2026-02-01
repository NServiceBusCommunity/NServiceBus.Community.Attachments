using System.Transactions;
using Microsoft.Data.SqlClient;
using NServiceBus.Attachments.Sql;
using NServiceBus.Pipeline;
using NServiceBus.Transport;

class ReceiveBehavior(Func<Cancel, Task<SqlConnection>> connectionBuilder, IPersister persister, bool useTransport, bool useSynchronizedStorage) :
    Behavior<IInvokeHandlerContext>
{
    StorageAccessor storageAccessor = new();

    public override Task Invoke(IInvokeHandlerContext context, Func<Task> next)
    {
        var state = BuildState(context);
        context.Extensions.Set(state);
        return next();
    }

    SqlAttachmentState BuildState(IInvokeHandlerContext context)
    {
        if (useSynchronizedStorage)
        {
            var session = context.SynchronizedStorageSession;
            if (session is not null)
            {
                if (storageAccessor.TryGetTransaction(session, out var transaction))
                {
                    return new(transaction, persister);
                }

                if (storageAccessor.TryGetConnection(session, out var connection))
                {
                    return new(connection, persister);
                }
            }
        }

        if (useTransport)
        {
            if (context.Extensions.TryGet<TransportTransaction>(out var transportTransaction))
            {
                if (transportTransaction.TryGet<Transaction>(out var transaction))
                {
                    return new(transaction, connectionBuilder, persister);
                }

                if (transportTransaction.TryGet("System.Data.SqlClient.SqlTransaction", out SqlTransaction? dbTransaction))
                {
                    return new(dbTransaction!, persister);
                }

                if (transportTransaction.TryGet("System.Data.SqlClient.SqlConnection", out SqlConnection? connection))
                {
                    return new(connection!, persister);
                }
            }
            else
            {
                throw new($"{nameof(AttachmentSettings.UseTransportConnectivity)} was configured but no {nameof(TransportTransaction)} could be found");
            }
        }

        return new(connectionBuilder, persister);
    }
}