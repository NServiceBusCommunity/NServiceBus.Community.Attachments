public static class Connection
{
    public static SqlInstance SqlInstance = new(
        "NServiceBusAttachments",
        async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                create table [dbo].[MessageAttachments](
                    Id uniqueidentifier default newsequentialid() primary key not null,
                    MessageId nvarchar(50) not null,
                    MessageIdLower as lower(MessageId),
                    Name nvarchar(255) not null,
                    NameLower as lower(Name),
                    Created datetime2(0) not null default sysutcdatetime(),
                    Expiry datetime2(0) not null,
                    Metadata nvarchar(max),
                    Data varbinary(max) not null
                );
                create unique index Index_MessageIdName
                    on [dbo].[MessageAttachments](MessageIdLower, NameLower);
                """;
            await command.ExecuteNonQueryAsync();
        });

    public static async Task CreateBusinessTable(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            create table [dbo].[BusinessEntities](
                Id uniqueidentifier default newsequentialid() primary key not null,
                Value nvarchar(max) not null
            );
            """;
        await command.ExecuteNonQueryAsync();
    }
}
