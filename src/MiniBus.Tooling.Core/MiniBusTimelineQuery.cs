namespace MiniBus.Tooling.Core;

public sealed record MiniBusTimelineQuery
{
    public string? MessageId { get; init; }

    public string? CorrelationId { get; init; }

    public DateTimeOffset? FromUtc { get; init; }

    public DateTimeOffset? ToUtc { get; init; }

    public int? Limit { get; init; }

    public MiniBusToolingValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(MessageId) && string.IsNullOrWhiteSpace(CorrelationId))
        {
            return MiniBusToolingValidationResult.Invalid(
                "A timeline query requires either a message id or correlation id.");
        }

        if (!string.IsNullOrWhiteSpace(MessageId) && !string.IsNullOrWhiteSpace(CorrelationId))
        {
            return MiniBusToolingValidationResult.Invalid(
                "A timeline query supports either message id or correlation id, not both.");
        }

        return MiniBusToolingQueryFilter.ValidateTimeWindowAndLimit(FromUtc, ToUtc, Limit);
    }
}
