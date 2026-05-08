using MiniBus.Core.Recoverability;

namespace MiniBus.AzureFunctions.Processing;

public sealed class MiniBusProcessorOptions
{
    public string EndpointName { get; set; } = "MiniBus";

    public bool EnableSagas { get; set; }

    public MiniBusRecoverabilityOptions Recoverability { get; } = new();
}
