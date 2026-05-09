using System.Data.Common;

namespace MiniBus.Persistence.Sql;

public sealed class MiniBusSqlPersistenceOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public Func<DbConnection>? ConnectionFactory { get; set; }

    public string SchemaName { get; set; } = "MiniBus";

    public string InboxTableName { get; set; } = "Inbox";

    public string OutboxTableName { get; set; } = "Outbox";

    public int DispatcherBatchSize { get; set; } = 100;
}
