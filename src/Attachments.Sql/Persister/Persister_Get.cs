using Microsoft.Data.SqlClient;

namespace NServiceBus.Attachments.Sql
#if Raw
    .Raw
#endif
    ;

public partial class Persister
{
    /// <inheritdoc />
    public virtual async Task<AttachmentString> GetString(string messageId, string name, SqlConnection connection, SqlTransaction? transaction, Encoding? encoding = null, Cancel cancel = default)
    {
        Guard.AgainstNullOrEmpty(messageId);
        Guard.AgainstNullOrEmpty(name);
        Guard.AgainstLongAttachmentName(name);
        await using var command = CreateGetDataCommand(messageId, name, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(SequentialAccess | SingleRow, cancel);
        if (await reader.ReadAsync(cancel))
        {
            var metadataString = reader.GetStringOrNull(1);
            var metadata = MetadataSerializer.Deserialize(metadataString);
            encoding = MetadataSerializer.GetEncoding(encoding, metadata);
            return new(name, reader.GetString(2, encoding), metadata);
        }

        throw ThrowNotFound(messageId, name);
    }

    /// <inheritdoc />
    public virtual async Task<AttachmentBytes> GetBytes(string messageId, string name, SqlConnection connection, SqlTransaction? transaction, Cancel cancel = default)
    {
        Guard.AgainstNullOrEmpty(messageId);
        Guard.AgainstNullOrEmpty(name);
        Guard.AgainstLongAttachmentName(name);
        await using var command = CreateGetDataCommand(messageId, name, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(SequentialAccess | SingleRow, cancel);
        if (await reader.ReadAsync(cancel))
        {
            var metadataString = reader.GetStringOrNull(1);
            var metadata = MetadataSerializer.Deserialize(metadataString);
            var bytes = (byte[]) reader[2];

            return new(name, bytes, metadata);
        }

        throw ThrowNotFound(messageId, name);
    }

    /// <inheritdoc />
    public virtual async Task<MemoryStream> GetMemoryStream(string messageId, string name, SqlConnection connection, SqlTransaction? transaction, Cancel cancel = default)
    {
        Guard.AgainstNullOrEmpty(messageId);
        Guard.AgainstNullOrEmpty(name);
        Guard.AgainstLongAttachmentName(name);
        await using var command = CreateGetDataCommand(messageId, name, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(SequentialAccess | SingleRow, cancel);
        if (await reader.ReadAsync(cancel))
        {
            var bytes = (byte[]) reader[2];

            return new(bytes);
        }

        throw ThrowNotFound(messageId, name);
    }

    /// <inheritdoc />
    public virtual async Task<AttachmentStream> GetStream(
        string messageId,
        string name,
        SqlConnection connection,
        SqlTransaction? transaction,
        bool disposeConnectionOnStreamDispose,
        Cancel cancel = default)
    {
        Guard.AgainstNullOrEmpty(messageId);
        Guard.AgainstNullOrEmpty(name);
        Guard.AgainstLongAttachmentName(name);
        // command/reader ownership is handed off to the returned AttachmentStream's cleanups so the
        // SqlSequentialStream stays valid until the caller disposes the AttachmentStream. await
        // using would dispose them at this method's exit, killing the stream before it is read.
        var command = CreateGetDataCommand(messageId, name, connection, transaction);
        SqlDataReader? reader = null;
        try
        {
            reader = await command.ExecuteReaderAsync(SequentialAccess | SingleRow, cancel);
            if (await reader.ReadAsync(cancel))
            {
                return InnerGetStream(name, reader, command, disposeConnectionOnStreamDispose);
            }
        }
        catch
        {
            if (reader is not null)
            {
                await reader.DisposeAsync();
            }
            await command.DisposeAsync();
            throw;
        }

        await reader.DisposeAsync();
        await command.DisposeAsync();
        if (disposeConnectionOnStreamDispose)
        {
            await connection.DisposeAsync();
        }
        throw ThrowNotFound(messageId, name);
    }

    /// <inheritdoc />
    public virtual async IAsyncEnumerable<AttachmentStream> GetStreams(
        string messageId,
        SqlConnection connection,
        SqlTransaction? transaction,
        [EnumeratorCancellation] Cancel cancel = default)
    {
        Guard.AgainstNullOrEmpty(messageId);
        await using var command = CreateGetDatasCommand(messageId, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(SequentialAccess, cancel);
        while (await reader.ReadAsync(cancel))
        {
            cancel.ThrowIfCancellationRequested();
            var name = reader.GetString(0);
            var length = reader.GetInt64(1);
            var metadata = MetadataSerializer.Deserialize(reader.GetStringOrNull(2));
            yield return new(name, reader.GetStream(3), length, metadata);
        }
    }

    /// <inheritdoc />
    public virtual async IAsyncEnumerable<AttachmentBytes> GetBytes(
        string messageId,
        SqlConnection connection,
        SqlTransaction? transaction,
        [EnumeratorCancellation] Cancel cancel = default)
    {
        Guard.AgainstNullOrEmpty(messageId);
        await using var command = CreateGetDatasCommand(messageId, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(SequentialAccess, cancel);
        while (await reader.ReadAsync(cancel))
        {
            cancel.ThrowIfCancellationRequested();
            var name = reader.GetString(0);
            var metadata = MetadataSerializer.Deserialize(reader.GetStringOrNull(2));
            var bytes = (byte[]) reader[3];
            yield return new(name, bytes, metadata);
        }
    }

    /// <inheritdoc />
    public virtual async IAsyncEnumerable<AttachmentString> GetStrings(string messageId, SqlConnection connection, SqlTransaction? transaction, Encoding? encoding = null, [EnumeratorCancellation] Cancel cancel = default)
    {
        Guard.AgainstNullOrEmpty(messageId);
        encoding = encoding.Default();
        await using var command = CreateGetDatasCommand(messageId, connection, transaction);
        await using var reader = await command.ExecuteReaderAsync(SequentialAccess, cancel);
        while (await reader.ReadAsync(cancel))
        {
            cancel.ThrowIfCancellationRequested();
            var name = reader.GetString(0);
            var metadata = MetadataSerializer.Deserialize(reader.GetStringOrNull(2));
            yield return new(name, reader.GetString(3, encoding), metadata);
        }
    }

    static AttachmentStream InnerGetStream(string name, SqlDataReader reader, SqlCommand command, bool disposeConnection)
    {
        var length = reader.GetInt64(0);
        var metadataString = reader.GetStringOrNull(1);
        var sqlStream = reader.GetStream(2);
        var metadata = MetadataSerializer.Deserialize(metadataString);
        if (disposeConnection)
        {
            return new(name, sqlStream, length, metadata, command.DisposeAsync, reader.DisposeAsync, command.Connection!.DisposeAsync);
        }

        return new(name, sqlStream, length, metadata, command.DisposeAsync, reader.DisposeAsync);
    }

    SqlCommand CreateGetDataCommand(string messageId, string name, SqlConnection connection, SqlTransaction? transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"""
            select
                datalength(Data),
                Metadata,
                Data
            from {table}
            where
                NameLower = lower(@Name) and
                MessageIdLower = lower(@MessageId)
            order by Id
            """;
        command.AddParameter("Name", name);
        command.AddParameter("MessageId", messageId);
        return command;
    }

    SqlCommand CreateGetDatasCommand(string messageId, SqlConnection connection, SqlTransaction? transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"""
            select
                Name,
                datalength(Data),
                Metadata,
                Data
            from {table}
            where
                MessageIdLower = lower(@MessageId)
            order by Id
            """;
        command.AddParameter("MessageId", messageId);
        return command;
    }
}