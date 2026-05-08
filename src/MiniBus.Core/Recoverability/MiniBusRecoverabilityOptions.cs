namespace MiniBus.Core.Recoverability;

public sealed class MiniBusRecoverabilityOptions
{
    public int ImmediateRetries { get; set; }

    public IList<TimeSpan> DelayedRetries { get; } = new List<TimeSpan>();

    public bool DeadLetterAfterRetriesExhausted { get; set; } = true;
}
