namespace MiniBus.Core.ClaimCheck;

public sealed class MiniBusClaimCheckOptions
{
    public bool Enabled { get; set; }

    public long PayloadThresholdBytes { get; set; } = 128 * 1024;

    public string Provider { get; set; } = MiniBusClaimCheckProviderNames.AzureBlobStorage;

    public static MiniBusClaimCheckOptions Disabled { get; } = new();

    public void Validate()
    {
        if (!Enabled)
        {
            return;
        }

        if (PayloadThresholdBytes < 0)
        {
            throw new MiniBusClaimCheckConfigurationException(
                "MiniBus claim-check payload threshold bytes cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(Provider))
        {
            throw new MiniBusClaimCheckConfigurationException(
                "MiniBus claim-check provider cannot be empty when claim-check behavior is enabled.");
        }
    }
}
