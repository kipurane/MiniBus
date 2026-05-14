using MiniBus.Core.Sagas;
using MiniBus.Core.Serialization;

namespace MiniBus.Persistence.Sql;

public sealed class SqlSagaDataSerializer
{
    private readonly IMessageSerializer _messageSerializer;

    public SqlSagaDataSerializer(IMessageSerializer messageSerializer)
    {
        _messageSerializer = messageSerializer;
    }

    public SerializedSagaData Serialize<TData>(TData data)
        where TData : class, ISagaData, new()
    {
        ArgumentNullException.ThrowIfNull(data);

        var dataType = GetDataTypeName(typeof(TData));
        return new SerializedSagaData(
            dataType,
            _messageSerializer.Serialize(data, typeof(TData)).ToArray());
    }

    public TData Deserialize<TData>(byte[] body)
        where TData : class, ISagaData, new()
    {
        ArgumentNullException.ThrowIfNull(body);

        var data = _messageSerializer.Deserialize(new BinaryData(body), typeof(TData));
        return data as TData
               ?? throw new InvalidOperationException($"MiniBus saga data type '{typeof(TData).FullName}' could not be deserialized.");
    }

    public static string GetDataTypeName(Type dataType)
    {
        ArgumentNullException.ThrowIfNull(dataType);
        return dataType.AssemblyQualifiedName ?? dataType.FullName ?? dataType.Name;
    }
}
