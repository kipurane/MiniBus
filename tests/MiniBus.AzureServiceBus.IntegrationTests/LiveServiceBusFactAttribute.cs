using Xunit.Abstractions;
using Xunit.Sdk;

namespace MiniBus.AzureServiceBus.IntegrationTests;

[TraitDiscoverer(
    "MiniBus.AzureServiceBus.IntegrationTests.LiveServiceBusTraitDiscoverer",
    "MiniBus.AzureServiceBus.IntegrationTests")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class LiveServiceBusFactAttribute : FactAttribute, ITraitAttribute
{
    public LiveServiceBusFactAttribute()
    {
        var eligibility = LiveServiceBusTestSettings.GetEligibility();
        if (!eligibility.IsEnabled)
        {
            Skip = eligibility.SkipReason;
        }
    }
}

public sealed class LiveServiceBusTraitDiscoverer : ITraitDiscoverer
{
    public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
    {
        yield return new KeyValuePair<string, string>("Category", LiveServiceBusTestSettings.Category);
    }
}
