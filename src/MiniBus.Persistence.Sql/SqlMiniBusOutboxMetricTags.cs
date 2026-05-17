namespace MiniBus.Persistence.Sql;

internal static class SqlMiniBusOutboxMetricTags
{
    // Stable metric tag names. Changing these should be treated as an observability contract change.
    public const string MiniBusOutboxOperationKind = "minibus.outbox_operation_kind";
    public const string MiniBusSqlOutboxDispatchOutcome = "minibus.sql_outbox.dispatch_outcome";
}
