namespace NServiceBus.Attachments.Sql
#if Raw
    .Raw
#endif
    ;

/// <summary>
/// Represents a table, schema, and optional database.
/// </summary>
public class Table
{
    /// <summary>
    /// Instantiates a new <see cref="Table" />.
    /// <paramref name="tableName" /> and <paramref name="schema" /> should be non sanitized.
    /// </summary>
    public Table(string tableName, string schema = "dbo") :
        this(tableName, schema, database: null, sanitize: true)
    {
    }

    /// <summary>
    /// Instantiates a new <see cref="Table" /> with a database name for fully qualified 3-part names.
    /// </summary>
    public Table(string tableName, string schema, string database) :
        this(tableName, schema, database, sanitize: true)
    {
    }

    /// <summary>
    /// Instantiates a new <see cref="Table" />.
    /// </summary>
    public Table(string tableName, string schema, string? database, bool sanitize)
    {
        Guard.AgainstNullOrEmpty(tableName);
        Guard.AgainstNullOrEmpty(schema);
        TableName = tableName;
        Schema = schema;
        Database = database;
        if (sanitize)
        {
            TableName = SqlSanitizer.Sanitize(TableName);
            Schema = SqlSanitizer.Sanitize(Schema);
            if (Database is not null)
            {
                Database = SqlSanitizer.Sanitize(Database);
            }
        }

        FullTableName = Database is not null
            ? $"{Database}.{Schema}.{TableName}"
            : $"{Schema}.{TableName}";
    }

    /// <summary>
    /// The sanitized fully qualified table name.
    /// </summary>
    public string FullTableName { get; }

    /// <summary>
    /// The sanitized table name.
    /// </summary>
    public string TableName { get; }

    /// <summary>
    /// The sanitized schema name.
    /// </summary>
    public string Schema { get; }

    /// <summary>
    /// The sanitized database name, or null if not specified.
    /// </summary>
    public string? Database { get; }

    /// <summary>
    /// Converts a string into a <see cref="Table" />.
    /// Assumes and un-sanitized table string with no schema.
    /// </summary>
    public static implicit operator Table(string table) =>
        new(table);

    /// <summary>
    /// Returns <see cref="FullTableName" />.
    /// </summary>
    public override string ToString() =>
        FullTableName;
}