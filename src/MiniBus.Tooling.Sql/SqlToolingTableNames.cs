namespace MiniBus.Tooling.Sql;

internal sealed class SqlToolingTableNames
{
    public SqlToolingTableNames(MiniBusSqlToolingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var schemaName = ValidateIdentifier(options.SchemaName, nameof(options.SchemaName));

        Inbox = Format(schemaName, ValidateIdentifier(options.InboxTableName, nameof(options.InboxTableName)));
        Outbox = Format(schemaName, ValidateIdentifier(options.OutboxTableName, nameof(options.OutboxTableName)));
        Saga = Format(schemaName, ValidateIdentifier(options.SagaTableName, nameof(options.SagaTableName)));
    }

    public string Inbox { get; }

    public string Outbox { get; }

    public string Saga { get; }

    private static string Format(string schemaName, string tableName)
    {
        return $"[{EscapeIdentifier(schemaName)}].[{EscapeIdentifier(tableName)}]";
    }

    private static string ValidateIdentifier(string identifier, string parameterName)
    {
        return string.IsNullOrWhiteSpace(identifier)
            ? throw new ArgumentException("SQL tooling table identifiers cannot be null, empty, or whitespace.", parameterName)
            : identifier;
    }

    private static string EscapeIdentifier(string identifier)
    {
        return identifier.Replace("]", "]]", StringComparison.Ordinal);
    }
}
