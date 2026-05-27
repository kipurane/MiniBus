using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace MiniBus.Tooling.Sql;

public sealed class MiniBusSqlToolingOptions
{
    public string? ConnectionString { get; set; }

    public Func<DbConnection>? ConnectionFactory { get; set; }

    public string SchemaName { get; set; } = "MiniBus";

    public string InboxTableName { get; set; } = "Inbox";

    public string OutboxTableName { get; set; } = "Outbox";

    public string SagaTableName { get; set; } = "Sagas";

    public int DefaultQueryLimit { get; set; } = 100;

    public DbConnection CreateConnection()
    {
        if (ConnectionFactory is not null)
        {
            return ConnectionFactory()
                   ?? throw new InvalidOperationException(
                       "MiniBus SQL tooling connection factory returned null.");
        }

        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException(
                "MiniBus SQL tooling requires a connection string or connection factory.");
        }

        return new SqlConnection(ConnectionString);
    }
}
