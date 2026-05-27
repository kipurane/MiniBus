namespace MiniBus.Tooling.Core;

public sealed record MiniBusOutboxDrainResult(
    bool IsSupported,
    bool Succeeded,
    int DispatchedCount,
    string? Message,
    string? Error)
{
    public static MiniBusOutboxDrainResult Success(int dispatchedCount)
    {
        return new MiniBusOutboxDrainResult(
            IsSupported: true,
            Succeeded: true,
            DispatchedCount: dispatchedCount,
            Message: $"Dispatched {dispatchedCount} SQL outbox operation(s).",
            Error: null);
    }

    public static MiniBusOutboxDrainResult Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new MiniBusOutboxDrainResult(
            IsSupported: true,
            Succeeded: false,
            DispatchedCount: 0,
            Message: null,
            Error: error);
    }

    public static MiniBusOutboxDrainResult Unsupported(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new MiniBusOutboxDrainResult(
            IsSupported: false,
            Succeeded: false,
            DispatchedCount: 0,
            Message: null,
            Error: reason);
    }
}
