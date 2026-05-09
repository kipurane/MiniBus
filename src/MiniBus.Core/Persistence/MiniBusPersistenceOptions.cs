namespace MiniBus.Core.Persistence;

public sealed class MiniBusPersistenceOptions
{
    public bool EnableInbox { get; set; } = true;

    public bool EnableOutbox { get; set; } = true;
}
