using System.Text.Json;
using MiniBus.Core.Persistence;
using MiniBus.Core.Serialization;

namespace MiniBus.Persistence.Sql;

public sealed class SqlOutboxOperationSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IMessageSerializer _messageSerializer;

    public SqlOutboxOperationSerializer(IMessageSerializer messageSerializer)
    {
        _messageSerializer = messageSerializer;
    }

    public SerializedOutboxOperation Serialize(MiniBusOutboxOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return new SerializedOutboxOperation(
            operation.Kind.ToString(),
            operation.MessageType.AssemblyQualifiedName ?? operation.MessageType.FullName ?? operation.MessageType.Name,
            _messageSerializer.Serialize(operation.Message, operation.MessageType).ToArray(),
            SerializeHeaders(operation.Headers),
            operation.DueTime);
    }

    public MiniBusOutboxStoredOperation Deserialize(
        Guid id,
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
