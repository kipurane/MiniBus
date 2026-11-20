using System.Globalization;
using MiniBus.Core.Headers;
using MiniBus.Core.Contracts;
using MiniBus.Core.Identity;
using MiniBus.Core.Serialization;

namespace MiniBus.Core.ClaimCheck;

public sealed class MiniBusClaimCheckMessageTransformer
{
    public const string DefaultContentType = "application/json";

    private readonly IMessageSerializer _serializer;
    private readonly MiniBusClaimCheckOptions _options;
    private readonly IMiniBusClaimCheckPayloadStore? _payloadStore;
    private readonly MiniBusMessageIdentityRegistry _messageIdentities;

    public MiniBusClaimCheckMessageTransformer(
        IMessageSerializer serializer,
        MiniBusClaimCheckOptions? options = null,
        IMiniBusClaimCheckPayloadStore? payloadStore = null,
        MiniBusMessageIdentityRegistry? messageIdentities = null)
    {
        _serializer = serializer;
        _options = options ?? MiniBusClaimCheckOptions.Disabled;
        _payloadStore = payloadStore;
        _messageIdentities = messageIdentities ?? new MiniBusMessageIdentityRegistry();
    }

    public MiniBusOutgoingMessage Transform(
        object message,
        Type messageType,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(messageType);
        if (_options.Enabled)
        {
            throw new InvalidOperationException(
                "MiniBus claim-check behavior requires asynchronous message preparation.");
        }

        return PrepareInline(message, messageType, headers);
    }

    public MiniBusOutgoingMessage PrepareStoredBody(
        BinaryData body,
        Type messageType,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(messageType);
        if (!typeof(IMessage).IsAssignableFrom(messageType))
        {
            throw new ArgumentException($"Type '{messageType.FullName}' must implement IMessage.", nameof(messageType));
        }

        var mappedHeaders = headers is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(headers, StringComparer.Ordinal);
        if (!mappedHeaders.ContainsKey(MiniBusHeaderNames.MessageType)
            || !mappedHeaders.ContainsKey(MiniBusHeaderNames.EnclosedMessageTypes))
        {
            var identity = _messageIdentities.GetIdentity(messageType);
            mappedHeaders.TryAdd(MiniBusHeaderNames.MessageType, identity);
            mappedHeaders.TryAdd(MiniBusHeaderNames.EnclosedMessageTypes, identity);
        }

        AddDefaultHeaders(mappedHeaders);
        return new MiniBusOutgoingMessage(body, mappedHeaders, MiniBusClaimCheckReferenceReader.IsClaimChecked(mappedHeaders));
    }

    public async Task<MiniBusOutgoingMessage> TransformAsync(
        object message,
        Type messageType,
        IReadOnlyDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        var outgoingMessage = PrepareInline(message, messageType, headers);
        var body = outgoingMessage.Body;
        if (!_options.Enabled || body.ToMemory().Length <= _options.PayloadThresholdBytes)
        {
            return outgoingMessage;
        }

        if (_payloadStore is null)
        {
            throw new MiniBusClaimCheckConfigurationException(
                "MiniBus claim-check behavior is enabled, but no claim-check payload store is configured.");
        }

        var mappedHeaders = new Dictionary<string, string>(outgoingMessage.Headers, StringComparer.Ordinal);
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

    private MiniBusOutgoingMessage PrepareInline(
        object message,
        Type messageType,
        IReadOnlyDictionary<string, string>? headers)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(messageType);
        if (!messageType.IsInstanceOfType(message))
        {
            throw new ArgumentException($"Message instance must be assignable to '{messageType.FullName}'.", nameof(message));
        }

        var mappedHeaders = CreateHeaders(messageType, headers, _messageIdentities);
        var body = _serializer.Serialize(message, messageType);
        _options.Validate();
        return new MiniBusOutgoingMessage(body, mappedHeaders, IsClaimChecked: false);
    }

    public static Dictionary<string, string> CreateHeaders(
        Type messageType,
        IReadOnlyDictionary<string, string>? headers,
        MiniBusMessageIdentityRegistry? messageIdentities = null)
    {
        var mappedHeaders = headers is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(headers, StringComparer.Ordinal);

        var messageTypeName = (messageIdentities ?? new MiniBusMessageIdentityRegistry()).GetIdentity(messageType);
        mappedHeaders[MiniBusHeaderNames.MessageType] = messageTypeName;
        mappedHeaders[MiniBusHeaderNames.EnclosedMessageTypes] = messageTypeName;
        AddDefaultHeaders(mappedHeaders);

        return mappedHeaders;
    }

    private static void AddDefaultHeaders(IDictionary<string, string> headers)
    {
        headers.TryAdd(MiniBusHeaderNames.MessageId, Guid.NewGuid().ToString("N"));
        headers.TryAdd(MiniBusHeaderNames.ContentType, DefaultContentType);
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
