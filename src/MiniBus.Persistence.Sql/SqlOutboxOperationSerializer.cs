using System.Text.Json;
using MiniBus.Core.ClaimCheck;
using MiniBus.Core.Persistence;
using MiniBus.Core.Serialization;

namespace MiniBus.Persistence.Sql;

public sealed class SqlOutboxOperationSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMessageSerializer _messageSerializer;
    private readonly MiniBusClaimCheckOptions _claimCheckOptions;
    private readonly MiniBusClaimCheckMessageTransformer _transformer;

    public SqlOutboxOperationSerializer(
        IMessageSerializer messageSerializer,
        MiniBusClaimCheckOptions? claimCheckOptions = null,
        IMiniBusClaimCheckPayloadStore? claimCheckPayloadStore = null)
    {
        _messageSerializer = messageSerializer;
        _claimCheckOptions = claimCheckOptions ?? MiniBusClaimCheckOptions.Disabled;
        _transformer = new MiniBusClaimCheckMessageTransformer(
            messageSerializer,
            _claimCheckOptions,
            claimCheckPayloadStore);
    }

    public SerializedOutboxOperation Serialize(MiniBusOutboxOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_claimCheckOptions.Enabled)
        {
            throw new InvalidOperationException(
                "MiniBus SQL outbox operation serialization requires SerializeAsync when claim-check behavior is enabled.");
        }

        var headers = MiniBusClaimCheckMessageTransformer.CreateHeaders(operation.MessageType, operation.Headers);

        return new SerializedOutboxOperation(
            operation.Kind.ToString(),
            operation.MessageType.AssemblyQualifiedName ?? operation.MessageType.FullName ?? operation.MessageType.Name,
            _messageSerializer.Serialize(operation.Message, operation.MessageType).ToArray(),
            SerializeHeaders(headers),
            operation.DueTime);
    }

    public async Task<SerializedOutboxOperation> SerializeAsync(
        MiniBusOutboxOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var outgoingMessage = await _transformer
            .TransformAsync(operation.Message, operation.MessageType, operation.Headers, cancellationToken)
            .ConfigureAwait(false);

        return new SerializedOutboxOperation(
            operation.Kind.ToString(),
            operation.MessageType.AssemblyQualifiedName ?? operation.MessageType.FullName ?? operation.MessageType.Name,
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
        var messageType = Type.GetType(messageTypeName, throwOnError: false)
            ?? throw new InvalidOperationException($"MiniBus outbox message type '{messageTypeName}' could not be resolved.");

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
