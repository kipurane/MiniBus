namespace MiniBus.Tooling.Core;

public sealed record MiniBusToolingQueryFilter
{
    public string? EndpointName { get; init; }

    public string? MessageId { get; init; }

    public string? CorrelationId { get; init; }

    public string? Status { get; init; }

    public DateTimeOffset? FromUtc { get; init; }

    public DateTimeOffset? ToUtc { get; init; }

    public int? Limit { get; init; }

    public MiniBusToolingValidationResult Validate()
    {
        return ValidateTimeWindowAndLimit(FromUtc, ToUtc, Limit);
    }

    public static MiniBusToolingValidationResult ValidateTimeWindowAndLimit(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit)
    {
        if (fromUtc is not null && toUtc is not null && fromUtc > toUtc)
        {
            return MiniBusToolingValidationResult.Invalid(
                "The start of the time window must be earlier than or equal to the end.");
        }

        if (limit is not null and <= 0)
        {
            return MiniBusToolingValidationResult.Invalid("Limit must be greater than zero.");
        }

        return MiniBusToolingValidationResult.Valid;
    }
}
