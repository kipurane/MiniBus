using System.Security.Cryptography;
using System.Text;

namespace MiniBus.AzureServiceBus.IntegrationTests;

internal sealed class LiveServiceBusNameFactory
{
    internal const string Prefix = "minibus-live";
    internal const int QueueAndTopicMaximumLength = 260;
    internal const int SubscriptionMaximumLength = 50;

    private readonly string _runToken;
    private int _sequence;

    public LiveServiceBusNameFactory(string? runToken = null)
    {
        _runToken = Normalize(runToken ?? Guid.NewGuid().ToString("N")[..10]);
    }

    public string Queue(string scenario) => Create("q", scenario, QueueAndTopicMaximumLength);

    public string Topic(string scenario) => Create("t", scenario, QueueAndTopicMaximumLength);

    public string Subscription(string scenario) => Create("s", scenario, SubscriptionMaximumLength);

    private string Create(string kind, string scenario, int maximumLength)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var normalizedScenario = Normalize(scenario);
        var raw = $"{Prefix}-{kind}-{_runToken}-{sequence:x}-{normalizedScenario}";
        if (raw.Length <= maximumLength)
        {
            return raw;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))
            .ToLowerInvariant()[..12];
        var retainedLength = maximumLength - hash.Length - 1;
        return $"{raw[..retainedLength]}-{hash}";
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        var previousWasSeparator = false;

        foreach (var character in value.ToLowerInvariant())
        {
            if (character is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                builder.Append(character);
                previousWasSeparator = false;
            }
            else if (!previousWasSeparator)
            {
                builder.Append('-');
                previousWasSeparator = true;
            }
        }

        return builder.ToString().Trim('-') switch
        {
            "" => "scenario",
            var normalized => normalized
        };
    }
}
