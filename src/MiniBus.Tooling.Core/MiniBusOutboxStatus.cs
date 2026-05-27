namespace MiniBus.Tooling.Core;

public enum MiniBusOutboxStatus
{
    Pending,
    Claimed,
    Dispatched,
    Failed
}
