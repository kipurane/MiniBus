namespace MiniBus.Persistence.Sql;

internal static class SqlMiniBusOutboxDispatchOutcomes
{
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Empty = "empty";
}
