namespace MiniBus.Core.Persistence;

public enum MiniBusOutboxOperationKind
{
    Send = 0,
    Publish = 1,
    Schedule = 2
}
