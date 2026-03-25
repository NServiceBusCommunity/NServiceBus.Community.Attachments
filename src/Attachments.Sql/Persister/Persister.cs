namespace NServiceBus.Attachments.Sql
#if Raw
    .Raw
#endif
    ;

/// <summary>
/// Raw access to manipulating attachments outside of the context of the NServiceBus pipeline.
/// </summary>
public partial class Persister :
    IPersister
{
    string table;

    /// <summary>
    /// Instantiate a new instance of <see cref="Persister" />.
    /// </summary>
    public Persister(string database, string schema = "dbo", string table = "MessageAttachments")
    {
        var sanitizedDatabase = SqlSanitizer.Sanitize(database);
        var sanitizedSchema = SqlSanitizer.Sanitize(schema);
        var sanitizedTable = SqlSanitizer.Sanitize(table);
        this.table = $"{sanitizedDatabase}.{sanitizedSchema}.{sanitizedTable}";
    }

    static Exception ThrowNotFound(string messageId, string name) => new($"Could not find attachment. MessageId:{messageId}, Name:{name}");
}