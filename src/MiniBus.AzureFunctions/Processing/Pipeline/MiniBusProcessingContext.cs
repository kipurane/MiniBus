using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using MiniBus.AzureFunctions.Settlement;
using MiniBus.Core.Persistence;
using MiniBus.Core.Recoverability;

namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal sealed class MiniBusProcessingContext
{
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public MiniBusProcessingContext(
        ServiceBusReceivedMessage message,
        MiniBusProcessorOptions options,
        IMiniBusMessageActions? actions = null)
    {
        Message = message;
        Body = message.Body;
        Options = options;
        Actions = actions;
    }

    public ServiceBusReceivedMessage Message { get; }

    public BinaryData Body { get; set; }

    public MiniBusProcessorOptions Options { get; }

    public IMiniBusMessageActions? Actions { get; }

    public IReadOnlyDictionary<string, string> Headers { get; set; } =
        EmptyHeaders;

    public Type? MessageType { get; set; }

    public object? DeserializedMessage { get; set; }

    public MiniBusReceivedMessageContext? HandlerContext { get; set; }

    public IMiniBusPersistenceSession? PersistenceSession { get; set; }

    public MiniBusInboxMessage? InboxMessage { get; set; }

    public MiniBusOutboxOperationCollector? OutboxCollector { get; set; }

    public IReadOnlyCollection<MiniBusOutboxOperation> OutboxOperations =>
        OutboxCollector?.Operations ?? Array.Empty<MiniBusOutboxOperation>();

    public RecoverabilityDecision? RecoverabilityDecision { get; set; }

    public MiniBusSettlementDecision SettlementDecision { get; set; } = MiniBusSettlementDecision.None();

    public Type? LastHandlerType { get; set; }

    public Type? LastSagaType { get; set; }

    public string? LastSagaCorrelationId { get; set; }

    public Activity? ProcessingActivity { get; set; }

    public bool IsShortCircuited { get; private set; }

    public void ShortCircuit()
    {
        IsShortCircuited = true;
    }
}
