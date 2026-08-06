namespace MiniBus.AzureServiceBus.IntegrationTests;

internal static class LiveServiceBusTestSettings
{
    public const string Category = "LiveAzureServiceBus";
    public const string OptInEnvironmentVariable = "MINIBUS_RUN_LIVE_AZURE_SERVICEBUS_TESTS";
    public const string ConnectionStringEnvironmentVariable = "MINIBUS_AZURE_SERVICEBUS_CONNECTION_STRING";

    public static LiveTestEligibility GetEligibility()
    {
        return Evaluate(
            Environment.GetEnvironmentVariable(OptInEnvironmentVariable),
            Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable));
    }

    public static LiveTestEligibility Evaluate(string? optIn, string? connectionString)
    {
        if (!bool.TryParse(optIn, out var enabled) || !enabled)
        {
            return LiveTestEligibility.Disabled(
                $"Set {OptInEnvironmentVariable}=true to run live Azure Service Bus tests.");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return LiveTestEligibility.Disabled(
                $"Set {ConnectionStringEnvironmentVariable} to a dedicated test namespace connection string.");
        }

        return LiveTestEligibility.Enabled();
    }

    public static string GetRequiredConnectionString()
    {
        var eligibility = GetEligibility();
        if (!eligibility.IsEnabled)
        {
            throw new InvalidOperationException(eligibility.SkipReason);
        }

        return Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)!;
    }
}

internal sealed record LiveTestEligibility(bool IsEnabled, string? SkipReason)
{
    public static LiveTestEligibility Enabled() => new(true, null);

    public static LiveTestEligibility Disabled(string reason) => new(false, reason);
}
