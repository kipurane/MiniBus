using MiniBus.Core.ClaimCheck;
using MiniBus.Core.Contracts;
using MiniBus.Core.Headers;
using MiniBus.Core.Serialization;
using Xunit;

namespace MiniBus.Core.Tests;

public sealed class ClaimCheckTests
{
    [Fact]
    public async Task Transformer_IsDisabledByDefault()
    {
        var store = new RecordingPayloadStore();
        var transformer = new MiniBusClaimCheckMessageTransformer(new RecordingSerializer("inline-body"), payloadStore: store);

        var message = await transformer.TransformAsync(new TestCommand(Guid.NewGuid()), typeof(TestCommand));

        Assert.False(message.IsClaimChecked);
        Assert.Equal("inline-body", message.Body.ToString());
        Assert.Empty(store.Writes);
    }

    [Fact]
    public async Task Transformer_KeepsBelowThresholdPayloadInline()
    {
        var store = new RecordingPayloadStore();
        var transformer = new MiniBusClaimCheckMessageTransformer(
            new RecordingSerializer("small"),
            new MiniBusClaimCheckOptions { Enabled = true, PayloadThresholdBytes = 5 },
            store);

        var message = await transformer.TransformAsync(new TestCommand(Guid.NewGuid()), typeof(TestCommand));

        Assert.False(message.IsClaimChecked);
        Assert.Equal("small", message.Body.ToString());
        Assert.Empty(store.Writes);
    }

    [Fact]
    public async Task Transformer_StoresAboveThresholdPayloadAndAddsHeaders()
    {
        var createdUtc = new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);
        var expiresUtc = createdUtc.AddHours(1);
        var store = new RecordingPayloadStore
        {
            Reference = new MiniBusClaimCheckPayloadReference(
                MiniBusClaimCheckProviderNames.AzureBlobStorage,
                "minibus-payloads",
                "payloads/2026/05/15/payload-1.bin",
                "payload-1",
                10,
                "application/json",
                createdUtc,
                expiresUtc)
        };
        var transformer = new MiniBusClaimCheckMessageTransformer(
            new RecordingSerializer("large-body"),
            new MiniBusClaimCheckOptions { Enabled = true, PayloadThresholdBytes = 3 },
            store);

        var message = await transformer.TransformAsync(
            new TestCommand(Guid.NewGuid()),
            typeof(TestCommand),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MiniBusHeaderNames.MessageId] = "message-1",
                [MiniBusHeaderNames.CorrelationId] = "correlation-1"
            });

        Assert.True(message.IsClaimChecked);
        Assert.DoesNotContain("large-body", message.Body.ToString(), StringComparison.Ordinal);
        Assert.Equal("large-body", Assert.Single(store.Writes).Payload.ToString());
        Assert.Equal(bool.TrueString, message.Headers[MiniBusClaimCheckHeaderNames.Enabled]);
        Assert.Equal(MiniBusClaimCheckProviderNames.AzureBlobStorage, message.Headers[MiniBusClaimCheckHeaderNames.Provider]);
        Assert.Equal("minibus-payloads", message.Headers[MiniBusClaimCheckHeaderNames.ContainerName]);
        Assert.Equal("payloads/2026/05/15/payload-1.bin", message.Headers[MiniBusClaimCheckHeaderNames.BlobName]);
        Assert.Equal("payload-1", message.Headers[MiniBusClaimCheckHeaderNames.PayloadId]);
        Assert.Equal("10", message.Headers[MiniBusClaimCheckHeaderNames.PayloadLength]);
        Assert.Equal("message-1", message.Headers[MiniBusHeaderNames.MessageId]);
        Assert.Equal("correlation-1", message.Headers[MiniBusHeaderNames.CorrelationId]);
        Assert.Equal(typeof(TestCommand).AssemblyQualifiedName, message.Headers[MiniBusHeaderNames.MessageType]);
        Assert.Equal("application/json", message.Headers[MiniBusHeaderNames.ContentType]);
    }

    [Fact]
    public async Task Transformer_RejectsEnabledClaimCheckWithoutStore()
    {
        var transformer = new MiniBusClaimCheckMessageTransformer(
            new RecordingSerializer("large-body"),
            new MiniBusClaimCheckOptions { Enabled = true, PayloadThresholdBytes = 1 });

        var exception = await Assert.ThrowsAsync<MiniBusClaimCheckConfigurationException>(
            () => transformer.TransformAsync(new TestCommand(Guid.NewGuid()), typeof(TestCommand)));

        Assert.Contains("payload store", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReferenceReader_RejectsInvalidPayloadLength()
    {
        var headers = CreateClaimCheckHeaders();
        headers[MiniBusClaimCheckHeaderNames.PayloadLength] = "not-a-number";

        var exception = Assert.Throws<MiniBusInvalidClaimCheckReferenceException>(
            () => MiniBusClaimCheckReferenceReader.Read(headers));

        Assert.Contains("payload length", exception.Message, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> CreateClaimCheckHeaders()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MiniBusClaimCheckHeaderNames.Enabled] = bool.TrueString,
            [MiniBusClaimCheckHeaderNames.Provider] = MiniBusClaimCheckProviderNames.AzureBlobStorage,
            [MiniBusClaimCheckHeaderNames.ContainerName] = "minibus-payloads",
            [MiniBusClaimCheckHeaderNames.BlobName] = "payloads/2026/05/15/payload-1.bin",
            [MiniBusClaimCheckHeaderNames.PayloadId] = "payload-1",
            [MiniBusClaimCheckHeaderNames.PayloadLength] = "10",
            [MiniBusClaimCheckHeaderNames.CreatedUtc] = "2026-05-15T12:00:00.0000000+00:00"
        };
    }

    private sealed record TestCommand(Guid Id) : ICommand;

    private sealed class RecordingSerializer : IMessageSerializer
    {
        private readonly string _body;

        public RecordingSerializer(string body)
        {
            _body = body;
        }

        public BinaryData Serialize(object message, Type messageType)
        {
            return BinaryData.FromString(_body);
        }

        public object Deserialize(BinaryData body, Type messageType)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingPayloadStore : IMiniBusClaimCheckPayloadStore
    {
        public MiniBusClaimCheckPayloadReference Reference { get; set; } =
            new(
                MiniBusClaimCheckProviderNames.AzureBlobStorage,
                "minibus-payloads",
                "payload.bin",
                "payload",
                0,
                null,
                DateTimeOffset.UtcNow,
                null);

        public List<(BinaryData Payload, MiniBusClaimCheckPayloadWriteOptions? Options)> Writes { get; } = new();

        public Task<MiniBusClaimCheckPayloadReference> WriteAsync(
            BinaryData payload,
            MiniBusClaimCheckPayloadWriteOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Writes.Add((payload, options));
            return Task.FromResult(Reference);
        }

        public Task<BinaryData> ReadAsync(
            MiniBusClaimCheckPayloadReference reference,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
