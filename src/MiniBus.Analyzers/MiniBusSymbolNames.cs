namespace MiniBus.Analyzers;

internal static class MiniBusSymbolNames
{
    public const string IMessage = "MiniBus.Core.Contracts.IMessage";
    public const string ICommand = "MiniBus.Core.Contracts.ICommand";
    public const string IEvent = "MiniBus.Core.Contracts.IEvent";
    public const string IHandleMessages = "MiniBus.Core.Handlers.IHandleMessages`1";
    public const string IHandleSagaMessages = "MiniBus.Core.Sagas.IHandleSagaMessages`1";
    public const string MiniBusSaga = "MiniBus.Core.Sagas.MiniBusSaga`1";
    public const string ISagaTimeout = "MiniBus.Core.Sagas.ISagaTimeout";
    public const string MiniBusContext = "MiniBus.Core.Context.MiniBusContext";
    public const string MiniBusProcessor = "MiniBus.AzureFunctions.Processing.MiniBusProcessor";
    public const string MiniBusProcessorOptions = "MiniBus.AzureFunctions.Processing.MiniBusProcessorOptions";
    public const string AzureServiceBusTransportRoutes = "MiniBus.AzureServiceBus.Routing.AzureServiceBusTransportRoutes";
    public const string MiniBusAzureFunctionsServiceCollectionExtensions =
        "MiniBus.AzureFunctions.DependencyInjection.MiniBusAzureFunctionsServiceCollectionExtensions";
}
