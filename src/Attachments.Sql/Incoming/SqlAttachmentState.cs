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
}
