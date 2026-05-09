using MiniBus.Core.Recoverability;
using MiniBus.Core.Persistence;

namespace MiniBus.AzureFunctions.Processing;

public sealed class MiniBusProcessorOptions
{
    public string EndpointName { get; set; } = "MiniBus";

    public bool EnableSagas { get; set; }

    public MiniBusPersistenceOptions Persistence { get; } = new();

    public MiniBusRecoverabilityOptions Recoverability { get; } = new();
}
