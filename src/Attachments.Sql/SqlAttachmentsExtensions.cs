using System.Data;
using Microsoft.Data.SqlClient;
using NServiceBus.Attachments.Sql;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Settings;

namespace NServiceBus;

/// <summary>
/// Extensions to enable and configure attachments.
/// </summary>
public static class SqlAttachmentsExtensions
{
    /// <summary>
    /// Enable SQL attachments for this endpoint.
    /// </summary>
    public static AttachmentSettings EnableAttachments(
        this EndpointConfiguration configuration,
        Func<SqlConnection> connectionFactory,
        GetTimeToKeep? timeToKeep = null,
        string database = "nservicebus",
        string schema = "dbo",
        string table = "MessageAttachments")
    {
        var dbConnection = connectionFactory();
        if (dbConnection.State == ConnectionState.Open)
        {
            throw new("This overload of EnableAttachments expects `Func<SqlConnection> connectionFactory` to return a un-opened SqlConnection.");
        }

        return EnableAttachments(
            configuration,
            connectionFactory: async cancel =>
            {
                var connection = connectionFactory();
                try
                {
                    await connection.OpenAsync(cancel);
                    return connection;
                }
                catch
                {
                    connection.Dispose();
                    throw;
                }
            },
            timeToKeep,
            database,
            schema,
            table);
    }

    /// <summary>
    /// Enable SQL attachments for this endpoint.
    /// </summary>
    public static AttachmentSettings EnableAttachments(
        this EndpointConfiguration configuration,
        string connection,
        GetTimeToKeep? timeToKeep = null,
        string database = "nservicebus",
        string schema = "dbo",
        string table = "MessageAttachments") =>
        EnableAttachments(
            configuration,
            connectionFactory: async cancel =>
            {
                var sqlConnection = new SqlConnection(connection);
                await sqlConnection.OpenAsync(cancel);
                return sqlConnection;
            },
            timeToKeep,
            database,
            schema,
            table);

    /// <summary>
    /// Enable SQL attachments for this endpoint.
    /// </summary>
    public static AttachmentSettings EnableAttachments(
        this EndpointConfiguration configuration,
        Func<Cancel, Task<SqlConnection>> connectionFactory,
        GetTimeToKeep? timeToKeep = null,
        string database = "nservicebus",
        string schema = "dbo",
        string table = "MessageAttachments")
    {
        var settings = configuration.GetSettings();
        var attachments = new AttachmentSettings(connectionFactory, timeToKeep ?? TimeToKeep.Default, database, schema, table);
        return SetAttachments(configuration, settings, attachments);
    }

    static AttachmentSettings SetAttachments(
        EndpointConfiguration configuration,
        SettingsHolder settings,
        AttachmentSettings attachments)
    {
        settings.Set(attachments);
        configuration.EnableFeature<AttachmentFeature>();
        configuration.DisableFeature<AttachmentsUsedWhenNotEnabledFeature>();
        return attachments;
    }
}