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
    public async Task Transformer_ThrowsWhenMessageIsNull()
    {
        var transformer = new MiniBusClaimCheckMessageTransformer(new RecordingSerializer("body"));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => transformer.TransformAsync(null!, typeof(TestCommand)));
    }

    [Fact]
    public async Task Transformer_ThrowsWhenMessageTypeIsNull()
    {
        var transformer = new MiniBusClaimCheckMessageTransformer(new RecordingSerializer("body"));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => transformer.TransformAsync(new TestCommand(Guid.NewGuid()), null!));
    }

    [Fact]
    public async Task Transformer_ThrowsWhenMessageIsNotAssignableToType()
    {
        var transformer = new MiniBusClaimCheckMessageTransformer(new RecordingSerializer("body"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => transformer.TransformAsync(new TestCommand(Guid.NewGuid()), typeof(string)));
    }

    [Fact]
    public async Task Transformer_RejectsProviderMismatch()
    {
        var store = new RecordingPayloadStore
        {
            Reference = new MiniBusClaimCheckPayloadReference(
                "wrong-provider",
                "container",
                "blob.bin",
                "payload-1",
                10,
                null,
                DateTimeOffset.UtcNow,
                null)
        };
        var transformer = new MiniBusClaimCheckMessageTransformer(
            new RecordingSerializer("large-body"),
            new MiniBusClaimCheckOptions
            {
                Enabled = true,
                PayloadThresholdBytes = 1,
                Provider = MiniBusClaimCheckProviderNames.AzureBlobStorage
            },
            store);

        var exception = await Assert.ThrowsAsync<MiniBusClaimCheckConfigurationException>(
            () => transformer.TransformAsync(new TestCommand(Guid.NewGuid()), typeof(TestCommand)));

        Assert.Contains("wrong-provider", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ClaimCheckOptions_Validate_ThrowsWhenThresholdIsNegative()
    {
        var options = new MiniBusClaimCheckOptions
        {
            Enabled = true,
            PayloadThresholdBytes = -1
        };

        Assert.Throws<MiniBusClaimCheckConfigurationException>(() => options.Validate());
    }

    [Fact]
    public void ClaimCheckOptions_Validate_ThrowsWhenProviderIsEmpty()
    {
        var options = new MiniBusClaimCheckOptions
        {
            Enabled = true,
            Provider = ""
        };

        Assert.Throws<MiniBusClaimCheckConfigurationException>(() => options.Validate());
    }

    [Fact]
    public void ClaimCheckOptions_Validate_SucceedsWhenDisabled()
    {
        var options = new MiniBusClaimCheckOptions
        {
            Enabled = false,
            PayloadThresholdBytes = -1,
            Provider = ""
        };

        options.Validate();
    }

    [Fact]
    public void ClaimCheckEnvelope_RoundTripsFromReference()
    {
        var createdUtc = new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);
        var expiresUtc = createdUtc.AddHours(24);
        var reference = new MiniBusClaimCheckPayloadReference(
            MiniBusClaimCheckProviderNames.AzureBlobStorage,
            "container",
            "blob.bin",
            "payload-1",
            256,
            "application/json",
            createdUtc,
            expiresUtc);

        var envelope = MiniBusClaimCheckEnvelope.FromReference(reference);

        Assert.Equal(reference.Provider, envelope.Provider);
        Assert.Equal(reference.ContainerName, envelope.ContainerName);
        Assert.Equal(reference.BlobName, envelope.BlobName);
        Assert.Equal(reference.PayloadId, envelope.PayloadId);
        Assert.Equal(reference.Length, envelope.PayloadLength);
        Assert.Equal(reference.CreatedUtc, envelope.CreatedUtc);
        Assert.Equal(reference.ExpiresUtc, envelope.ExpiresUtc);
    }

    [Fact]
    public void ClaimCheckEnvelope_ToBinaryData_ProducesValidJson()
    {
        var envelope = new MiniBusClaimCheckEnvelope(
            MiniBusClaimCheckProviderNames.AzureBlobStorage,
            "container",
            "blob.bin",
            "payload-1",
            256,
            "application/json",
            new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero),
            null);

        var data = envelope.ToBinaryData();

        var json = data.ToString();
        Assert.NotEmpty(json);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("payload-1", doc.RootElement.GetProperty("payloadId").GetString());
        Assert.Equal(256, doc.RootElement.GetProperty("payloadLength").GetInt64());
    }

    [Fact]
    public void Transformer_CreateHeaders_AddsRequiredHeadersWhenNoneProvided()
    {
        var headers = MiniBusClaimCheckMessageTransformer.CreateHeaders(typeof(TestCommand), null);

        Assert.True(headers.ContainsKey(MiniBusHeaderNames.MessageType));
        Assert.True(headers.ContainsKey(MiniBusHeaderNames.EnclosedMessageTypes));
        Assert.True(headers.ContainsKey(MiniBusHeaderNames.MessageId));
        Assert.Equal(MiniBusClaimCheckMessageTransformer.DefaultContentType, headers[MiniBusHeaderNames.ContentType]);
    }

    [Fact]
    public void Transformer_CreateHeaders_DoesNotOverwriteExistingHeaders()
    {
        var existingHeaders = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MiniBusHeaderNames.MessageId] = "existing-message-id",
            [MiniBusHeaderNames.ContentType] = "text/plain"
        };

        var headers = MiniBusClaimCheckMessageTransformer.CreateHeaders(typeof(TestCommand), existingHeaders);

        Assert.Equal("existing-message-id", headers[MiniBusHeaderNames.MessageId]);
        Assert.Equal("text/plain", headers[MiniBusHeaderNames.ContentType]);
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

    [Fact]
    public void ReferenceReader_IsClaimChecked_ReturnsFalseWhenHeaderAbsent()
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);

        Assert.False(MiniBusClaimCheckReferenceReader.IsClaimChecked(headers));
    }

    [Fact]
    public void ReferenceReader_IsClaimChecked_ReturnsFalseWhenHeaderIsFalse()
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MiniBusClaimCheckHeaderNames.Enabled] = bool.FalseString
        };

        Assert.False(MiniBusClaimCheckReferenceReader.IsClaimChecked(headers));
    }

    [Fact]
    public void ReferenceReader_IsClaimChecked_ReturnsTrueWhenHeaderIsTrue()
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MiniBusClaimCheckHeaderNames.Enabled] = bool.TrueString
        };

        Assert.True(MiniBusClaimCheckReferenceReader.IsClaimChecked(headers));
    }

    [Fact]
    public void ReferenceReader_Read_ReturnsCompleteReferenceWithAllFields()
    {
        var createdUtc = new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);
        var expiresUtc = createdUtc.AddHours(24);
        var headers = CreateClaimCheckHeaders();
        headers[MiniBusClaimCheckHeaderNames.ExpiresUtc] = expiresUtc.ToString("O");
        headers[MiniBusClaimCheckHeaderNames.ContentType] = "application/json";

        var reference = MiniBusClaimCheckReferenceReader.Read(headers);

        Assert.Equal(MiniBusClaimCheckProviderNames.AzureBlobStorage, reference.Provider);
        Assert.Equal("minibus-payloads", reference.ContainerName);
        Assert.Equal("payloads/2026/05/15/payload-1.bin", reference.BlobName);
        Assert.Equal("payload-1", reference.PayloadId);
        Assert.Equal(10, reference.Length);
        Assert.Equal("application/json", reference.ContentType);
        Assert.Equal(createdUtc, reference.CreatedUtc);
        Assert.Equal(expiresUtc, reference.ExpiresUtc);
    }

    [Theory]
    [InlineData(MiniBusClaimCheckHeaderNames.Provider)]
    [InlineData(MiniBusClaimCheckHeaderNames.ContainerName)]
    [InlineData(MiniBusClaimCheckHeaderNames.BlobName)]
    [InlineData(MiniBusClaimCheckHeaderNames.PayloadId)]
    [InlineData(MiniBusClaimCheckHeaderNames.PayloadLength)]
    [InlineData(MiniBusClaimCheckHeaderNames.CreatedUtc)]
    public void ReferenceReader_Read_ThrowsWhenRequiredHeaderIsMissing(string missingHeader)
    {
        var headers = CreateClaimCheckHeaders();
        headers.Remove(missingHeader);

        var exception = Assert.Throws<MiniBusInvalidClaimCheckReferenceException>(
            () => MiniBusClaimCheckReferenceReader.Read(headers));

        Assert.Contains(missingHeader, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReferenceReader_Read_ThrowsWhenCreatedUtcIsInvalid()
    {
        var headers = CreateClaimCheckHeaders();
        headers[MiniBusClaimCheckHeaderNames.CreatedUtc] = "not-a-date";

        var exception = Assert.Throws<MiniBusInvalidClaimCheckReferenceException>(
            () => MiniBusClaimCheckReferenceReader.Read(headers));

        Assert.Contains("created UTC", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReferenceReader_Read_ThrowsWhenExpiresUtcIsInvalid()
    {
        var headers = CreateClaimCheckHeaders();
        headers[MiniBusClaimCheckHeaderNames.ExpiresUtc] = "not-a-date";

        var exception = Assert.Throws<MiniBusInvalidClaimCheckReferenceException>(
            () => MiniBusClaimCheckReferenceReader.Read(headers));

        Assert.Contains("expires UTC", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReferenceReader_Read_ReturnsNullExpiresUtcWhenHeaderAbsent()
    {
        var headers = CreateClaimCheckHeaders();

        var reference = MiniBusClaimCheckReferenceReader.Read(headers);

        Assert.Null(reference.ExpiresUtc);
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
