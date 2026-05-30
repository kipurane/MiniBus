using MiniBus.Tooling.Core;

namespace MiniBus.Tooling.Web;

public sealed class MiniBusToolingQueryParameters
{
    public string? Endpoint { get; init; }

    public string? MessageId { get; init; }

    public string? CorrelationId { get; init; }

    public string? Status { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public int? Limit { get; init; }

    public MiniBusToolingQueryFilter ToFilter()
    {
        return new MiniBusToolingQueryFilter
        {
            EndpointName = Endpoint,
            MessageId = MessageId,
            CorrelationId = CorrelationId,
            Status = Status,
            FromUtc = From,
            ToUtc = To,
            Limit = Limit
        };
    }
}
