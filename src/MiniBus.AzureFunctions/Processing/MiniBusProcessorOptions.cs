using MiniBus.Core.Recoverability;
using MiniBus.Core.Persistence;
using MiniBus.Core.Identity;

namespace MiniBus.AzureFunctions.Processing;

public sealed class MiniBusProcessorOptions
{
    public string EndpointName { get; set; } = "MiniBus";

    public bool EnableSagas { get; set; }

    public MiniBusPersistenceOptions Persistence { get; } = new();

    public MiniBusRecoverabilityOptions Recoverability { get; } = new();

    public MiniBusAuditOptions Audit { get; } = new();

    public MiniBusMessageIdentityRegistry MessageIdentities { get; } = new();
}
