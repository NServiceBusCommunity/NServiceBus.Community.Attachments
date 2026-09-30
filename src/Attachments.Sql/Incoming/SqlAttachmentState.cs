using System.Transactions;
using Microsoft.Data.SqlClient;
using NServiceBus.Attachments.Sql;

class SqlAttachmentState
{
    Func<Cancel, Task<SqlConnection>> connectionFactory;
    public IPersister Persister;
    public Transaction? Transaction;
    public SqlTransaction? SqlTransaction;
    public SqlConnection? SqlConnection;

    public SqlAttachmentState(Func<Cancel, Task<SqlConnection>> connectionFactory, IPersister persister)
    {
        this.connectionFactory = connectionFactory;
        Persister = persister;
    }

    public SqlAttachmentState(SqlConnection connection, Func<Cancel, Task<SqlConnection>> connectionFactory, IPersister persister)
        : this(connectionFactory, persister) =>
        SqlConnection = connection;

    public SqlAttachmentState(SqlTransaction transaction, Func<Cancel, Task<SqlConnection>> connectionFactory, IPersister persister)
        : this(connectionFactory, persister) =>
        SqlTransaction = transaction;

    public SqlAttachmentState(Transaction transaction, Func<Cancel, Task<SqlConnection>> connectionFactory, IPersister persister)
        : this(connectionFactory, persister) =>
        Transaction = transaction;

    public Task<SqlConnection> GetConnection(Cancel cancel)
    {
        try
        {
            return connectionFactory(cancel);
        }
        catch (Exception exception)
        {
            throw new("Provided ConnectionFactory threw an exception", exception);
        }
    }

    /// <summary>
    /// Runs <paramref name="action"/> on the connection, and transaction, of the current receive when one is available.
    /// Otherwise runs it on a new connection from the factory.
    /// </summary>
    public async Task<T> Execute<T>(Func<SqlConnection, SqlTransaction?, Task<T>> action, Cancel cancel)
    {
        if (Transaction is not null)
        {
            await using var connectionFromState = await GetConnection(cancel);
            connectionFromState.EnlistTransaction(Transaction);
            return await action(connectionFromState, null);
        }

        if (SqlTransaction is not null)
        {
            return await action(SqlTransaction.Connection!, SqlTransaction);
        }

        if (SqlConnection is not null)
        {
            return await action(SqlConnection, null);
        }

        await using var connection = await GetConnection(cancel);
        return await action(connection, null);
    }

    /// <inheritdoc cref="Execute{T}"/>
    public Task Execute(Func<SqlConnection, SqlTransaction?, Task> action, Cancel cancel) =>
        Execute(
            async (connection, transaction) =>
            {
                await action(connection, transaction);
                return true;
            },
            cancel);
}
