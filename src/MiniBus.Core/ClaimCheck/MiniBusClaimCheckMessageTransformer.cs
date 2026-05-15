using System.Globalization;
using MiniBus.Core.Headers;
using MiniBus.Core.Serialization;

namespace MiniBus.Core.ClaimCheck;

public sealed class MiniBusClaimCheckMessageTransformer
{
    public const string DefaultContentType = "application/json";

    private readonly IMessageSerializer _serializer;
    private readonly MiniBusClaimCheckOptions _options;
    private readonly IMiniBusClaimCheckPayloadStore? _payloadStore;

    public MiniBusClaimCheckMessageTransformer(
        IMessageSerializer serializer,
        MiniBusClaimCheckOptions? options = null,
        IMiniBusClaimCheckPayloadStore? payloadStore = null)
    {
        _serializer = serializer;
        _options = options ?? MiniBusClaimCheckOptions.Disabled;
        _payloadStore = payloadStore;
    }

    public async Task<MiniBusOutgoingMessage> TransformAsync(
        object message,
        Type messageType,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(messageType);

        if (!messageType.IsInstanceOfType(message))
        {
            throw new ArgumentException($"Message instance must be assignable to '{messageType.FullName}'.", nameof(message));
        }

        var mappedHeaders = CreateHeaders(messageType, headers);
        var body = _serializer.Serialize(message, messageType);
        _options.Validate();

        if (!_options.Enabled || body.ToMemory().Length <= _options.PayloadThresholdBytes)
        {
            return new MiniBusOutgoingMessage(body, mappedHeaders, IsClaimChecked: false);
        }

        if (_payloadStore is null)
        {
            throw new MiniBusClaimCheckConfigurationException(
                "MiniBus claim-check behavior is enabled, but no claim-check payload store is configured.");
        }

        var contentType = mappedHeaders[MiniBusHeaderNames.ContentType];
        var reference = await _payloadStore
            .WriteAsync(
                body,
                new MiniBusClaimCheckPayloadWriteOptions { ContentType = contentType },
                cancellationToken)
            .ConfigureAwait(false);

        if (!string.Equals(reference.Provider, _options.Provider, StringComparison.Ordinal))
        {
            throw new MiniBusClaimCheckConfigurationException(
                $"MiniBus claim-check provider '{reference.Provider}' does not match configured provider '{_options.Provider}'.");
        }

        AddClaimCheckHeaders(mappedHeaders, reference);
        return new MiniBusOutgoingMessage(
            MiniBusClaimCheckEnvelope.FromReference(reference).ToBinaryData(),
            mappedHeaders,
            IsClaimChecked: true);
    }

    public static Dictionary<string, string> CreateHeaders(
        Type messageType,
        IReadOnlyDictionary<string, string>? headers)
    {
        var mappedHeaders = headers is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(headers, StringComparer.Ordinal);

        var messageTypeName = messageType.AssemblyQualifiedName ?? messageType.FullName ?? messageType.Name;
        mappedHeaders.TryAdd(MiniBusHeaderNames.MessageType, messageTypeName);
        mappedHeaders.TryAdd(MiniBusHeaderNames.EnclosedMessageTypes, messageTypeName);
        mappedHeaders.TryAdd(MiniBusHeaderNames.MessageId, Guid.NewGuid().ToString("N"));
        mappedHeaders.TryAdd(MiniBusHeaderNames.ContentType, DefaultContentType);

        return mappedHeaders;
    }

    private static void AddClaimCheckHeaders(
        IDictionary<string, string> headers,
        MiniBusClaimCheckPayloadReference reference)
    {
        headers[MiniBusClaimCheckHeaderNames.Enabled] = bool.TrueString;
        headers[MiniBusClaimCheckHeaderNames.Provider] = reference.Provider;
        headers[MiniBusClaimCheckHeaderNames.ContainerName] = reference.ContainerName;
        headers[MiniBusClaimCheckHeaderNames.BlobName] = reference.BlobName;
        headers[MiniBusClaimCheckHeaderNames.PayloadId] = reference.PayloadId;
        headers[MiniBusClaimCheckHeaderNames.PayloadLength] = reference.Length.ToString(CultureInfo.InvariantCulture);
        headers[MiniBusClaimCheckHeaderNames.CreatedUtc] = reference.CreatedUtc.ToString("O", CultureInfo.InvariantCulture);

        if (!string.IsNullOrWhiteSpace(reference.ContentType))
        {
            headers[MiniBusClaimCheckHeaderNames.ContentType] = reference.ContentType;
        }

        if (reference.ExpiresUtc is not null)
        {
            headers[MiniBusClaimCheckHeaderNames.ExpiresUtc] = reference.ExpiresUtc.Value.ToString("O", CultureInfo.InvariantCulture);
        }
    }
}
