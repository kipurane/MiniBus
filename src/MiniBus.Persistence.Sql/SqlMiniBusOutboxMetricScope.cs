namespace MiniBus.Persistence.Sql;

internal sealed class SqlMiniBusOutboxMetricScope
{
    public SqlMiniBusOutboxMetricScope(long startTimestamp)
    {
        StartTimestamp = startTimestamp;
    }

    public long StartTimestamp { get; }
}
