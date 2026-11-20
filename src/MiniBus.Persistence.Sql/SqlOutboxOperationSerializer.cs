using System.Text.Json;
using MiniBus.Core.ClaimCheck;
using MiniBus.Core.Persistence;
using MiniBus.Core.Serialization;
using MiniBus.Core.Identity;
using MiniBus.Core.Headers;

namespace MiniBus.Persistence.Sql;

public sealed class SqlOutboxOperationSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly MiniBusClaimCheckMessageTransformer _transformer;
    private readonly MiniBusMessageIdentityRegistry _messageIdentities;

    public SqlOutboxOperationSerializer(
        IMessageSerializer messageSerializer,
        MiniBusClaimCheckOptions? claimCheckOptions = null,
        IMiniBusClaimCheckPayloadStore? claimCheckPayloadStore = null,
        MiniBusMessageIdentityRegistry? messageIdentities = null)
    {
        _messageIdentities = messageIdentities ?? new MiniBusMessageIdentityRegistry();
        _transformer = new MiniBusClaimCheckMessageTransformer(
            messageSerializer,
            claimCheckOptions,
            claimCheckPayloadStore,
            _messageIdentities);
    }

    public SerializedOutboxOperation Serialize(MiniBusOutboxOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var outgoingMessage = _transformer.Transform(operation.Message, operation.MessageType, operation.Headers);
        return CreateStoredOperation(operation, outgoingMessage);
    }

    public async Task<SerializedOutboxOperation> SerializeAsync(
        MiniBusOutboxOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var outgoingMessage = await _transformer
            .TransformAsync(operation.Message, operation.MessageType, operation.Headers, cancellationToken)
            .ConfigureAwait(false);

        return CreateStoredOperation(operation, outgoingMessage);
    }

    private static SerializedOutboxOperation CreateStoredOperation(
        MiniBusOutboxOperation operation,
        MiniBusOutgoingMessage outgoingMessage)
    {
        return new SerializedOutboxOperation(
            operation.Kind.ToString(),
            outgoingMessage.Headers[MiniBusHeaderNames.MessageType],
            outgoingMessage.Body.ToArray(),
            SerializeHeaders(outgoingMessage.Headers),
            operation.DueTime);
    }

    public MiniBusOutboxStoredOperation Deserialize(
        Guid id,
        string outgoingMessageId,
        string operationKind,
        string messageTypeName,
        byte[] body,
        string headersJson,
        DateTimeOffset? dueTime,
        int attemptCount)
    {
        Type messageType;
        try
        {
            messageType = _messageIdentities.Resolve(messageTypeName);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw new InvalidOperationException($"MiniBus outbox message type '{messageTypeName}' could not be resolved.", exception);
        }

        return new MiniBusOutboxStoredOperation(
            id,
            outgoingMessageId,
            Enum.Parse<MiniBusOutboxOperationKind>(operationKind, ignoreCase: false),
            new BinaryData(body),
            messageType,
            DeserializeHeaders(headersJson),
            dueTime,
            attemptCount);
    }

    public static string SerializeHeaders(IReadOnlyDictionary<string, string> headers)
    {
        return JsonSerializer.Serialize(headers, JsonOptions);
    }

    public static IReadOnlyDictionary<string, string> DeserializeHeaders(string headersJson)
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson, JsonOptions)
               ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
