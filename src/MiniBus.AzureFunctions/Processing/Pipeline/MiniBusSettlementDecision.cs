namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal enum MiniBusSettlementDecisionKind
{
    None,
    Complete,
    DeadLetter,
    DelayedRetry
}

internal sealed record MiniBusSettlementDecision(
    MiniBusSettlementDecisionKind Kind,
    string? DeadLetterReason = null,
    string? DeadLetterDescription = null)
{
    public static MiniBusSettlementDecision None()
    {
        return new MiniBusSettlementDecision(MiniBusSettlementDecisionKind.None);
    }

    public static MiniBusSettlementDecision Complete()
    {
        return new MiniBusSettlementDecision(MiniBusSettlementDecisionKind.Complete);
    }

    public static MiniBusSettlementDecision DeadLetter(
        string deadLetterReason,
        string? deadLetterDescription)
    {
        return new MiniBusSettlementDecision(
            MiniBusSettlementDecisionKind.DeadLetter,
            deadLetterReason,
            deadLetterDescription);
    }

    public static MiniBusSettlementDecision DelayedRetry()
    {
        return new MiniBusSettlementDecision(MiniBusSettlementDecisionKind.DelayedRetry);
    }
}
