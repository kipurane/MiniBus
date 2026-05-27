namespace MiniBus.Tooling.Core;

public sealed record MiniBusToolingQueryResult<T>(
    bool IsSupported,
    string? UnsupportedReason,
    IReadOnlyList<T> Records)
{
    public static MiniBusToolingQueryResult<T> Success(IReadOnlyList<T> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return new MiniBusToolingQueryResult<T>(true, null, records);
    }

    public static MiniBusToolingQueryResult<T> Unsupported(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new MiniBusToolingQueryResult<T>(false, reason, Array.Empty<T>());
    }
}
