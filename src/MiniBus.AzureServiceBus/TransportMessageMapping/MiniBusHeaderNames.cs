namespace MiniBus.AzureServiceBus.TransportMessageMapping;

public static class MiniBusHeaderNames
{
    public const string MessageId = Core.Headers.MiniBusHeaderNames.MessageId;
    public const string MessageType = Core.Headers.MiniBusHeaderNames.MessageType;
    public const string EnclosedMessageTypes = Core.Headers.MiniBusHeaderNames.EnclosedMessageTypes;
    public const string CorrelationId = Core.Headers.MiniBusHeaderNames.CorrelationId;
    public const string CausationId = Core.Headers.MiniBusHeaderNames.CausationId;
    public const string ContentType = Core.Headers.MiniBusHeaderNames.ContentType;
}
