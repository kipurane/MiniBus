namespace MiniBus.Core.Serialization;

public interface IMessageSerializer
{
    BinaryData Serialize(object message, Type messageType);

    object Deserialize(BinaryData body, Type messageType);
}

