using Microsoft.Data.SqlClient;
using NServiceBus.Transport;

namespace NServiceBus.Attachments.Sql;

/// <summary>
/// All settings for attachments
/// </summary>
public partial class AttachmentSettings
{
    internal Func<Cancel, Task<SqlConnection>> ConnectionFactory;
    internal string Database;
    internal string Schema;
    internal string TableName;
    internal bool InstallerDisabled;
    internal bool RunEarlyCleanup = true;
    internal bool UseTransport;
    internal bool UseSynchronizedStorage;

    internal AttachmentSettings(Func<Cancel, Task<SqlConnection>> connectionFactory, GetTimeToKeep timeToKeep, string database, string schema, string table)
    {
        TimeToKeep = timeToKeep;
        ConnectionFactory = connectionFactory;
        Database = database;
        Schema = schema;
        TableName = table;
    }

    /// <summary>
    /// Use the ambient <see cref="TransportTransaction" /> to obtain a <see cref="SqlConnection" /> or <see cref="SqlTransaction" />.
    /// </summary>
    public void UseTransportConnectivity() =>
        UseTransport = true;

    /// <summary>
    /// Use the ambient <see cref="IMessageHandlerContext.SynchronizedStorageSession" /> to obtain a <see cref="SqlConnection" /> or <see cref="SqlTransaction" />.
    /// </summary>
    public void UseSynchronizedStorageSessionConnectivity() =>
        UseSynchronizedStorage = true;

    /// <summary>
    /// Disable the table creation installer.
    /// </summary>
    public void DisableInstaller() =>
        InstallerDisabled = true;

    /// <summary>
    /// Disable the attachment on Send or Reply.
    /// </summary>
    public void DisableEarlyCleanup() =>
        RunEarlyCleanup = false;
}