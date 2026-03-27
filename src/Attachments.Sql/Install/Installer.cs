using Microsoft.Data.SqlClient;

namespace NServiceBus.Attachments.Sql
#if Raw
    .Raw
#endif
    ;

/// <summary>
/// Used to take control over the storage table creation.
/// </summary>
public static class Installer
{
    /// <summary>
    /// Create the attachments storage table.
    /// </summary>
    public static async Task CreateTable(SqlConnection connection, string? database = null, string schema = "dbo", string table = "MessageAttachments", Cancel cancel = default)
    {
        database ??= new SqlConnectionStringBuilder(connection.ConnectionString).InitialCatalog;
        await using var command = connection.CreateCommand();
        command.CommandText = GetTableSql();
        command.AddParameter("schema", SqlSanitizer.Sanitize(schema));
        command.AddParameter("table", SqlSanitizer.Sanitize(table));
        command.AddParameter("database", SqlSanitizer.Sanitize(database));
        await command.ExecuteNonQueryAsync(cancel);
    }

    /// <summary>
    /// Get the sql used to create the attachments storage table.
    /// </summary>
    public static string GetTableSql()
    {
        using var stream = AssemblyHelper.Current.GetManifestResourceStream("Table.sql")!;
        using var streamReader = new StreamReader(stream);
        return streamReader.ReadToEnd();
    }
}