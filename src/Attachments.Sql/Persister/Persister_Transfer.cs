using Microsoft.Data.SqlClient;

namespace NServiceBus.Attachments.Sql
#if Raw
    .Raw
#endif
    ;

public partial class Persister
{
    /// <inheritdoc />
    public virtual async Task<Guid> Transfer(string sourceMessageId, string name, SqlConnection connection, SqlTransaction? transaction, string targetMessageId, string? targetName = null, DateTime? expiry = null, bool replace = false, Cancel cancel = default)
    {
        Guard.AgainstNullOrEmpty(sourceMessageId);
        Guard.AgainstNullOrEmpty(targetMessageId);
        Guard.AgainstNullOrEmpty(name);
        Guard.AgainstLongAttachmentName(name);
        if (targetName is not null)
        {
            Guard.AgainstNullOrEmpty(targetName);
            Guard.AgainstLongAttachmentName(targetName);
        }

        await using var command = CreateTransferCommand(sourceMessageId, name, targetMessageId, targetName, expiry, replace, connection, transaction);
        object? result;
        try
        {
            result = await command.ExecuteScalarAsync(cancel);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new($"Could not transfer attachment. An attachment named '{targetName ?? name}' already exists for '{targetMessageId}'. MessageId:{sourceMessageId}, Name:{name}", exception);
        }

        if (result is Guid id)
        {
            return id;
        }

        throw ThrowNotFound(sourceMessageId, name);
    }

    SqlCommand CreateTransferCommand(string sourceMessageId, string name, string targetMessageId, string? targetName, DateTime? expiry, bool replace, SqlConnection connection, SqlTransaction? transaction)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        // When replacing, remove any attachment the target already has with the same name.
        // Only delete when the source exists, so a missing source does not lose the existing attachment.
        // The source row itself is excluded in case the source and target are the same.
        var deleteExisting = "";
        if (replace)
        {
            deleteExisting =
                $"""
                delete from {table}
                where
                    MessageIdLower = lower(@TargetMessageId) and
                    NameLower = lower(coalesce(@TargetName, @Name)) and
                    not (MessageIdLower = lower(@SourceMessageId) and NameLower = lower(@Name)) and
                    exists (
                        select 1 from {table}
                        where
                            NameLower = lower(@Name) and
                            MessageIdLower = lower(@SourceMessageId));

                """;
        }

        command.CommandText =
            $"""
            {deleteExisting}update {table}
            set
                MessageId = @TargetMessageId,
                Name = coalesce(@TargetName, Name),
                Expiry = coalesce(@Expiry, Expiry)
            output inserted.Id
            where
                NameLower = lower(@Name) and
                MessageIdLower = lower(@SourceMessageId);
            """;
        command.AddParameter("Name", name);
        command.AddParameter("SourceMessageId", sourceMessageId);
        command.AddParameter("TargetMessageId", targetMessageId);
        command.AddParameter("TargetName", targetName);
        command.AddParameter("Expiry", expiry);
        return command;
    }
}
