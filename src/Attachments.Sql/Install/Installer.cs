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
    public static Task CreateTable(SqlConnection connection, Cancel cancel = default) =>
        CreateTable(connection, "MessageAttachments", cancel);

    /// <summary>
    /// Create the attachments storage table.
    /// </summary>
    public static async Task CreateTable(SqlConnection connection, Table table, Cancel cancel = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = GetTableSql();
        command.AddParameter("schema", table.Schema);
        command.AddParameter("table", table.TableName);
        command.AddParameter("database", table.Database);
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