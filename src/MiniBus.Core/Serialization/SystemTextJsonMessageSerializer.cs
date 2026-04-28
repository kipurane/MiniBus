using System.Text.Json;

namespace MiniBus.Core.Serialization;

public sealed class SystemTextJsonMessageSerializer : IMessageSerializer
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public SystemTextJsonMessageSerializer(JsonSerializerOptions? jsonSerializerOptions = null)
    {
        _jsonSerializerOptions = jsonSerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }

    public BinaryData Serialize(object message, Type messageType)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(messageType);

        var json = JsonSerializer.Serialize(message, messageType, _jsonSerializerOptions);
        return BinaryData.FromString(json);
    }

    public object Deserialize(BinaryData body, Type messageType)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(messageType);

        using var stream = body.ToStream();
        return JsonSerializer.Deserialize(stream, messageType, _jsonSerializerOptions)
               ?? throw new InvalidOperationException($"Failed to deserialize message body as {messageType.FullName}.");
    }
}

