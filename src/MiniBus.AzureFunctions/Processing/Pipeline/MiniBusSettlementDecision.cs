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
    private static readonly MiniBusSettlementDecision NoneDecision = new(MiniBusSettlementDecisionKind.None);
    private static readonly MiniBusSettlementDecision CompleteDecision = new(MiniBusSettlementDecisionKind.Complete);
    private static readonly MiniBusSettlementDecision DelayedRetryDecision = new(MiniBusSettlementDecisionKind.DelayedRetry);

    public static MiniBusSettlementDecision None()
    {
        return NoneDecision;
    }

    public static MiniBusSettlementDecision Complete()
    {
        return CompleteDecision;
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
        return DelayedRetryDecision;
    }
}
