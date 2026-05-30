using MiniBus.Tooling.Core;
using Microsoft.AspNetCore.Mvc;

namespace MiniBus.Tooling.Web;

public static class MiniBusToolingWebEndpoints
{
    private const string UnsupportedTitle = "Tooling provider cannot answer this request.";
    private const string InvalidTitle = "Invalid tooling query.";

    public static IEndpointRouteBuilder MapMiniBusToolingWebApi(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var api = endpoints.MapGroup("/api/tooling")
            .WithTags("MiniBus Tooling");

        api.MapGet("/inbox", ListInboxAsync)
            .WithName("ListMiniBusInboxRecords");
        api.MapGet("/inbox/{messageId}", GetInboxDetailAsync)
            .WithName("GetMiniBusInboxRecord");

        api.MapGet("/outbox", ListOutboxAsync)
            .WithName("ListMiniBusOutboxRecords");
        api.MapGet("/outbox/{messageId}", GetOutboxDetailAsync)
            .WithName("GetMiniBusOutboxRecord");

        api.MapGet("/sagas", ListSagasAsync)
            .WithName("ListMiniBusSagaRecords");
        api.MapGet("/sagas/{correlationId}", GetSagaDetailAsync)
            .WithName("GetMiniBusSagaRecord");

        api.MapGet("/timeline/message/{messageId}", GetMessageTimelineAsync)
            .WithName("GetMiniBusMessageTimeline");
        api.MapGet("/timeline/correlation/{correlationId}", GetCorrelationTimelineAsync)
            .WithName("GetMiniBusCorrelationTimeline");

        return endpoints;
    }

    public static async Task<IResult> ListInboxAsync(
        [AsParameters]
        MiniBusToolingQueryParameters parameters,
        [FromServices]
        IMiniBusInboxToolingReader reader,
        CancellationToken cancellationToken = default)
    {
        var result = await reader.ListAsync(parameters.ToFilter(), cancellationToken).ConfigureAwait(false);
        return ToQueryResult(result);
    }

    public static async Task<IResult> GetInboxDetailAsync(
        string messageId,
        [AsParameters]
        MiniBusToolingQueryParameters parameters,
        [FromServices]
        IMiniBusInboxToolingReader reader,
        CancellationToken cancellationToken = default)
    {
        var result = await reader.ListAsync(
                parameters.ToFilter() with { MessageId = messageId, Limit = 1 },
                cancellationToken)
            .ConfigureAwait(false);
        return ToDetailResult(result, record => string.Equals(record.MessageId, messageId, StringComparison.Ordinal));
    }

    public static async Task<IResult> ListOutboxAsync(
        [AsParameters]
        MiniBusToolingQueryParameters parameters,
        [FromServices]
        IMiniBusOutboxToolingReader reader,
        CancellationToken cancellationToken = default)
    {
        var result = await reader.ListAsync(parameters.ToFilter(), cancellationToken).ConfigureAwait(false);
        return ToQueryResult(result);
    }

    public static async Task<IResult> GetOutboxDetailAsync(
        string messageId,
        [AsParameters]
        MiniBusToolingQueryParameters parameters,
        [FromServices]
        IMiniBusOutboxToolingReader reader,
        CancellationToken cancellationToken = default)
    {
        var result = await reader.ListAsync(
                parameters.ToFilter() with { MessageId = messageId, Limit = 1 },
                cancellationToken)
            .ConfigureAwait(false);
        return ToDetailResult(
            result,
            record => string.Equals(record.OutgoingMessageId, messageId, StringComparison.Ordinal)
                      || string.Equals(record.IncomingMessageId, messageId, StringComparison.Ordinal));
    }

    public static async Task<IResult> ListSagasAsync(
        [AsParameters]
        MiniBusToolingQueryParameters parameters,
        [FromServices]
        IMiniBusSagaToolingReader reader,
        CancellationToken cancellationToken = default)
    {
        var result = await reader.ListAsync(parameters.ToFilter(), cancellationToken).ConfigureAwait(false);
        return ToQueryResult(result);
    }

    public static async Task<IResult> GetSagaDetailAsync(
        string correlationId,
        [AsParameters]
        MiniBusToolingQueryParameters parameters,
        [FromServices]
        IMiniBusSagaToolingReader reader,
        CancellationToken cancellationToken = default)
    {
        var result = await reader.ListAsync(
                parameters.ToFilter() with { CorrelationId = correlationId, Limit = 1 },
                cancellationToken)
            .ConfigureAwait(false);
        return ToDetailResult(
            result,
            record => string.Equals(record.CorrelationId, correlationId, StringComparison.Ordinal));
    }

    public static async Task<IResult> GetMessageTimelineAsync(
        string messageId,
        [AsParameters]
        MiniBusToolingQueryParameters parameters,
        [FromServices]
        IMiniBusTimelineToolingReader reader,
        CancellationToken cancellationToken = default)
    {
        var query = CreateTimelineQuery(parameters) with { MessageId = messageId };
        return await ReadTimelineAsync(query, reader, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<IResult> GetCorrelationTimelineAsync(
        string correlationId,
        [AsParameters]
        MiniBusToolingQueryParameters parameters,
        [FromServices]
        IMiniBusTimelineToolingReader reader,
        CancellationToken cancellationToken = default)
    {
        var query = CreateTimelineQuery(parameters) with { CorrelationId = correlationId };
        return await ReadTimelineAsync(query, reader, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IResult> ReadTimelineAsync(
        MiniBusTimelineQuery query,
        IMiniBusTimelineToolingReader reader,
        CancellationToken cancellationToken)
    {
        var validation = query.Validate();
        if (!validation.IsValid)
        {
            return Results.Problem(validation.Error, statusCode: StatusCodes.Status400BadRequest, title: InvalidTitle);
        }

        var timeline = await reader.ReadAsync(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(timeline);
    }

    private static MiniBusTimelineQuery CreateTimelineQuery(MiniBusToolingQueryParameters parameters)
    {
        return new MiniBusTimelineQuery
        {
            FromUtc = parameters.From,
            ToUtc = parameters.To,
            Limit = parameters.Limit
        };
    }

    private static IResult ToQueryResult<T>(MiniBusToolingQueryResult<T> result)
    {
        return result.IsSupported
            ? Results.Ok(result.Records)
            : Results.Problem(
                result.UnsupportedReason,
                statusCode: StatusCodes.Status400BadRequest,
                title: UnsupportedTitle);
    }

    private static IResult ToDetailResult<T>(
        MiniBusToolingQueryResult<T> result,
        Func<T, bool> predicate)
    {
        if (!result.IsSupported)
        {
            return Results.Problem(
                result.UnsupportedReason,
                statusCode: StatusCodes.Status400BadRequest,
                title: UnsupportedTitle);
        }

        var record = result.Records.FirstOrDefault(predicate);
        return record is null ? Results.NotFound() : Results.Ok(record);
    }
}
