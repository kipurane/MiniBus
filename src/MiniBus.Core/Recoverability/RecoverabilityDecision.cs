namespace MiniBus.Core.Recoverability;

public sealed record RecoverabilityDecision(
    RecoverabilityDecisionKind Kind,
    IReadOnlyDictionary<string, string> Headers,
    Exception Exception,
    int ImmediateAttempt,
    int DelayedAttempt,
    TimeSpan? Delay = null,
    string? DeadLetterReason = null,
    string? DeadLetterDescription = null);
