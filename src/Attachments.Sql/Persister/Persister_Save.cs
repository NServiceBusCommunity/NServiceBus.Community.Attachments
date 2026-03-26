using Microsoft.Data.SqlClient;

namespace NServiceBus.Attachments.Sql
#if Raw
    .Raw
#endif
    ;

public partial class Persister
{
    /// <inheritdoc />
    public virtual Task<Guid> SaveStream(SqlConnection connection, SqlTransaction? transaction, string messageId, string name, DateTime expiry, Stream stream, IReadOnlyDictionary<string, string>? metadata = null, Cancel cancel = default)
    {
        Guard.AgainstNullOrEmpty(messageId);
        Guard.AgainstNullOrEmpty(name);
        Guard.AgainstLongAttachmentName(name);
        stream.MoveToStart();
        return Save(connection, transaction, messageId, name, expiry, stream, metadata, cancel);
    }

    /// <inheritdoc />
    public virtual async Task<Guid> SaveString(SqlConnection connection, SqlTransaction? transaction, string messageId, string name, DateTime expiry, string value, Encoding? encoding = null, IReadOnlyDictionary<string, string>? metadata = null, Cancel cancel = default)
    {
        Guard.AgainstNullOrEmpty(messageId);
        Guard.AgainstNullOrEmpty(name);
        Guard.AgainstLongAttachmentName(name);
        encoding = encoding.Default();
        var dictionary = MetadataSerializer.AppendEncoding(encoding, metadata);
        var result = PipeHelper.StartWriter(
            async stream =>
            {
                await using var writer = new StreamWriter(stream, encoding, leaveOpen: true);
                await writer.WriteAsync(value);
            }, cancel);
        var readerStream = result.readerStream;
        await using (readerStream)
        {
            var guid = await Save(connection, transaction, messageId, name, expiry, readerStream, dictionary, cancel);
            await result.writerTask;
            return guid;
        }
    }

    async Task<Guid> Save(SqlConnection connection, SqlTransaction? transaction, string messageId, string name, DateTime expiry, Stream stream, IReadOnlyDictionary<string, string>? metadata = null, Cancel cancel = default)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"""
            insert into {table}
            (
                MessageId,
                Name,
                Expiry,
                Data,
                Metadata
            )
            output inserted.ID
            values
            (
                @MessageId,
                @Name,
                @Expiry,
                @Data,
                @Metadata
            )
            """;
        command.AddParameter("MessageId", messageId);
        command.AddParameter("Name", name);
        command.AddParameter("Expiry", expiry);
        command.AddParameter("Metadata", MetadataSerializer.Serialize(metadata));
        command.AddBinary("Data", stream);

        // Send the data to the server asynchronously
        return (Guid) (await command.ExecuteScalarAsync(cancel))!;
    }
}