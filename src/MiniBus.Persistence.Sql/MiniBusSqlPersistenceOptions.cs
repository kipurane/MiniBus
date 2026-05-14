using System.Data.Common;

namespace MiniBus.Persistence.Sql;

public sealed class MiniBusSqlPersistenceOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public Func<DbConnection>? ConnectionFactory { get; set; }

    public string SchemaName { get; set; } = "MiniBus";

    public string InboxTableName { get; set; } = "Inbox";

    public string OutboxTableName { get; set; } = "Outbox";

    public string SagaTableName { get; set; } = "Sagas";

    public int DispatcherBatchSize { get; set; } = 100;

    public TimeSpan OutboxClaimLeaseDuration { get; set; } = TimeSpan.FromMinutes(5);

    public TimeSpan? InboxRetention { get; set; }

    public TimeSpan? DispatchedOutboxRetention { get; set; }

    public TimeSpan? FailedOutboxRetention { get; set; }

    public int CleanupBatchSize { get; set; } = 1000;
}
