namespace MiniBus.Core.Recoverability;

public enum RecoverabilityDecisionKind
{
    ImmediateRetry,
    DelayedRetry,
    DeadLetter,
    Propagate
}
