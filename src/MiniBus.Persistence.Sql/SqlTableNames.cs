namespace MiniBus.Persistence.Sql;

internal sealed class SqlTableNames
{
    public SqlTableNames(MiniBusSqlPersistenceOptions options)
    {
        Inbox = Format(options.SchemaName, options.InboxTableName);
        Outbox = Format(options.SchemaName, options.OutboxTableName);
        Sagas = Format(options.SchemaName, options.SagaTableName);
    }

    public string Inbox { get; }

    public string Outbox { get; }

    public string Sagas { get; }

    private static string Format(string schemaName, string tableName)
    {
        return $"{QuoteIdentifier(schemaName)}.{QuoteIdentifier(tableName)}";
    }

    private static string QuoteIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("SQL identifiers cannot be empty.", nameof(value));
        }

        return $"[{value.Replace("]", "]]", StringComparison.Ordinal)}]";
    }
}
