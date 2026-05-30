using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.Tooling.Core;
using MiniBus.Tooling.Web;

namespace MiniBus.Tooling.Web.Tests;

public sealed class MiniBusToolingWebEndpointTests
{
    [Fact]
    public async Task ListInbox_ReturnsReadOnlyToolingRecords()
    {
        var reader = new RecordingToolingReader(
            inbox: new[]
            {
                new MiniBusInboxRecord(
                    "Billing",
                    "message-1",
                    new DateTimeOffset(2026, 5, 27, 10, 0, 0, TimeSpan.Zero),
                    "correlation-1",
                    new Dictionary<string, string>())
            });

        var result = await MiniBusToolingWebEndpoints.ListInboxAsync(
            new MiniBusToolingQueryParameters { Endpoint = "Billing" },
            reader);

        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Contains("message-1", response.Body, StringComparison.Ordinal);
        Assert.Equal("Billing", reader.LastInboxFilter?.EndpointName);
    }

    [Fact]
    public async Task DetailEndpoint_ReturnsMatchingRecordOrNotFound()
    {
        var reader = new RecordingToolingReader(
            outbox: new[]
            {
                new MiniBusOutboxRecord(
                    Guid.Parse("4d7477b4-0d49-4b78-9d63-7f9f21b2d3f8"),
                    "outgoing-1",
                    "Billing",
                    "incoming-1",
                    "publish",
                    "InvoiceCreated",
                    null,
                    new DateTimeOffset(2026, 5, 27, 10, 0, 0, TimeSpan.Zero),
                    null,
                    null,
                    0,
                    null,
                    MiniBusOutboxStatus.Pending,
                    new Dictionary<string, string>())
            });

        var found = await MiniBusToolingWebEndpoints.GetOutboxDetailAsync(
            "outgoing-1",
            new MiniBusToolingQueryParameters(),
            reader);
        var missing = await MiniBusToolingWebEndpoints.GetOutboxDetailAsync(
            "missing",
            new MiniBusToolingQueryParameters(),
            reader);

        Assert.Equal(StatusCodes.Status200OK, (await ExecuteAsync(found)).StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, (await ExecuteAsync(missing)).StatusCode);
        Assert.Equal(1, reader.LastOutboxFilter?.Limit);
    }

    [Fact]
    public async Task TimelineEndpoint_ReturnsFragmentsAndSourceAvailability()
    {
        var reader = new RecordingToolingReader(
            timeline: new MiniBusMessageTimeline(
                new MiniBusTimelineQuery { CorrelationId = "correlation-1" },
                new[]
                {
                    new MiniBusTimelineFragment(
                        MiniBusTimelineSource.Inbox,
                        "processed",
                        new DateTimeOffset(2026, 5, 27, 10, 0, 0, TimeSpan.Zero),
                        "Inbox processed message-1",
                        new Dictionary<string, string>())
                },
                new[]
                {
                    new MiniBusTimelineSourceAvailability(MiniBusTimelineSource.Inbox, true),
                    new MiniBusTimelineSourceAvailability(
                        MiniBusTimelineSource.Broker,
                        false,
                        "Azure Service Bus tooling provider is not configured.")
                }));

        var result = await MiniBusToolingWebEndpoints.GetCorrelationTimelineAsync(
            "correlation-1",
            new MiniBusToolingQueryParameters(),
            reader);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Contains("Inbox processed message-1", response.Body, StringComparison.Ordinal);
        Assert.Contains("Azure Service Bus tooling provider is not configured.", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnsupportedProvider_ReturnsProblemWithoutCredentials()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MiniBus:Tooling:Sql:ConnectionString"] = ""
                })
                .Build());
        services.AddMiniBusToolingWeb(services.BuildServiceProvider().GetRequiredService<IConfiguration>());
        await using var provider = services.BuildServiceProvider();
        var reader = provider.GetRequiredService<IMiniBusInboxToolingReader>();

        var result = await MiniBusToolingWebEndpoints.ListInboxAsync(
            new MiniBusToolingQueryParameters(),
            reader);
        var response = await ExecuteAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.DoesNotContain("Password=", response.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApiSurface_ExposesOnlyGetRoutes()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        app.MapMiniBusToolingWebApi();

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/tooling", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.NotEmpty(routes);
        Assert.All(routes, route =>
        {
            var methods = route.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
            Assert.NotNull(methods);
            Assert.All(methods!, method => Assert.Equal(HttpMethods.Get, method));
        });
        Assert.DoesNotContain(routes, route => ContainsMutatingSegment(route.RoutePattern.RawText));
    }

    private static bool ContainsMutatingSegment(string? route)
    {
        return route is not null
               && new[] { "drain", "retry", "resubmit", "replay", "delete", "purge" }
                   .Any(segment => route.Contains(segment, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<(int StatusCode, string Body)> ExecuteAsync(IResult result)
    {
        var context = new DefaultHttpContext();
        context.RequestServices = new ServiceCollection()
            .AddLogging()
            .AddOptions()
            .BuildServiceProvider();
        await using var body = new MemoryStream();
        context.Response.Body = body;

        await result.ExecuteAsync(context);

        body.Position = 0;
        using var reader = new StreamReader(body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private sealed class RecordingToolingReader :
        IMiniBusInboxToolingReader,
        IMiniBusOutboxToolingReader,
        IMiniBusSagaToolingReader,
        IMiniBusTimelineToolingReader
    {
        private readonly IReadOnlyList<MiniBusInboxRecord> _inbox;
        private readonly IReadOnlyList<MiniBusOutboxRecord> _outbox;
        private readonly IReadOnlyList<MiniBusSagaRecord> _sagas;
        private readonly MiniBusMessageTimeline _timeline;

        public RecordingToolingReader(
            IReadOnlyList<MiniBusInboxRecord>? inbox = null,
            IReadOnlyList<MiniBusOutboxRecord>? outbox = null,
            IReadOnlyList<MiniBusSagaRecord>? sagas = null,
            MiniBusMessageTimeline? timeline = null)
        {
            _inbox = inbox ?? Array.Empty<MiniBusInboxRecord>();
            _outbox = outbox ?? Array.Empty<MiniBusOutboxRecord>();
            _sagas = sagas ?? Array.Empty<MiniBusSagaRecord>();
            _timeline = timeline ?? new MiniBusMessageTimeline(
                new MiniBusTimelineQuery { MessageId = "message-1" },
                Array.Empty<MiniBusTimelineFragment>(),
                Array.Empty<MiniBusTimelineSourceAvailability>());
        }

        public MiniBusToolingQueryFilter? LastInboxFilter { get; private set; }

        public MiniBusToolingQueryFilter? LastOutboxFilter { get; private set; }

        public Task<MiniBusToolingQueryResult<MiniBusInboxRecord>> ListAsync(
            MiniBusToolingQueryFilter filter,
            CancellationToken cancellationToken = default)
        {
            LastInboxFilter = filter;
            return Task.FromResult(MiniBusToolingQueryResult<MiniBusInboxRecord>.Success(_inbox));
        }

        Task<MiniBusToolingQueryResult<MiniBusOutboxRecord>> IMiniBusOutboxToolingReader.ListAsync(
            MiniBusToolingQueryFilter filter,
            CancellationToken cancellationToken)
        {
            LastOutboxFilter = filter;
            return Task.FromResult(MiniBusToolingQueryResult<MiniBusOutboxRecord>.Success(_outbox));
        }

        Task<MiniBusToolingQueryResult<MiniBusSagaRecord>> IMiniBusSagaToolingReader.ListAsync(
            MiniBusToolingQueryFilter filter,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(MiniBusToolingQueryResult<MiniBusSagaRecord>.Success(_sagas));
        }

        public Task<MiniBusMessageTimeline> ReadAsync(
            MiniBusTimelineQuery query,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_timeline);
        }
    }
}
