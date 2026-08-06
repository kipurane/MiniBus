using System.Diagnostics;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;

namespace MiniBus.AzureServiceBus.IntegrationTests;

internal sealed class LiveServiceBusScope : IAsyncDisposable
{
    private readonly ServiceBusAdministrationClient _administrationClient;
    private readonly List<CreatedEntity> _createdEntities = [];
    private readonly string _connectionString;

    public LiveServiceBusScope(string scenario)
    {
        _connectionString = LiveServiceBusTestSettings.GetRequiredConnectionString();
        Names = new LiveServiceBusNameFactory();
        Scenario = scenario;
        Client = new ServiceBusClient(_connectionString);
        _administrationClient = new ServiceBusAdministrationClient(_connectionString);
    }

    public string Scenario { get; }

    public LiveServiceBusNameFactory Names { get; }

    public ServiceBusClient Client { get; }

    public IReadOnlyList<string> CleanupFailures => _cleanupFailures;

    private readonly List<string> _cleanupFailures = [];

    public async Task<string> CreateQueueAsync(
        bool requiresDuplicateDetection = false,
        CancellationToken cancellationToken = default)
    {
        var name = Names.Queue(Scenario);
        var options = new CreateQueueOptions(name)
        {
            LockDuration = TimeSpan.FromSeconds(30),
            MaxDeliveryCount = 3,
            RequiresDuplicateDetection = requiresDuplicateDetection
        };
        if (requiresDuplicateDetection)
        {
            options.DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(1);
        }

        await _administrationClient.CreateQueueAsync(options, cancellationToken).ConfigureAwait(false);
        _createdEntities.Add(new CreatedEntity(EntityKind.Queue, name, null));
        return name;
    }

    public async Task<TopicSubscription> CreateTopicSubscriptionAsync(
        CancellationToken cancellationToken = default)
    {
        var topic = Names.Topic(Scenario);
        var subscription = Names.Subscription(Scenario);

        await _administrationClient.CreateTopicAsync(topic, cancellationToken).ConfigureAwait(false);
        _createdEntities.Add(new CreatedEntity(EntityKind.Topic, topic, null));

        await _administrationClient
            .CreateSubscriptionAsync(
                new CreateSubscriptionOptions(topic, subscription)
                {
                    LockDuration = TimeSpan.FromSeconds(30),
                    MaxDeliveryCount = 3
                },
                cancellationToken)
            .ConfigureAwait(false);
        _createdEntities.Add(new CreatedEntity(EntityKind.Subscription, topic, subscription));

        return new TopicSubscription(topic, subscription);
    }

    public ServiceBusReceiver CreateQueueReceiver(string queue)
    {
        return Client.CreateReceiver(
            queue,
            new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock });
    }

    public ServiceBusReceiver CreateSubscriptionReceiver(TopicSubscription entity)
    {
        return Client.CreateReceiver(
            entity.Topic,
            entity.Subscription,
            new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock });
    }

    public ServiceBusReceiver CreateDeadLetterReceiver(string queue)
    {
        return Client.CreateReceiver(
            queue,
            new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock,
                SubQueue = SubQueue.DeadLetter
            });
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var entity in _createdEntities.AsEnumerable().Reverse())
        {
            try
            {
                switch (entity.Kind)
                {
                    case EntityKind.Subscription:
                        await _administrationClient
                            .DeleteSubscriptionAsync(entity.Name, entity.ChildName!)
                            .ConfigureAwait(false);
                        break;
                    case EntityKind.Topic:
                        await _administrationClient.DeleteTopicAsync(entity.Name).ConfigureAwait(false);
                        break;
                    case EntityKind.Queue:
                        await _administrationClient.DeleteQueueAsync(entity.Name).ConfigureAwait(false);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception exception)
            {
                var message = LiveServiceBusDiagnostics.Redact(exception.Message, _connectionString);
                _cleanupFailures.Add($"{entity.Kind} '{entity.SafeAlias}': {message}");
                Debug.WriteLine($"MiniBus live test cleanup failed for {entity.SafeAlias}: {message}");
            }
        }

        try
        {
            await Client.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            var message = LiveServiceBusDiagnostics.Redact(exception.Message, _connectionString);
            _cleanupFailures.Add($"Service Bus client: {message}");
            Debug.WriteLine($"MiniBus live test client cleanup failed: {message}");
        }

        LiveServiceBusCleanupException.ThrowIfAny(_cleanupFailures);
    }

    private enum EntityKind
    {
        Queue,
        Topic,
        Subscription
    }

    private sealed record CreatedEntity(EntityKind Kind, string Name, string? ChildName)
    {
        public string SafeAlias => ChildName is null ? Name : $"{Name}/{ChildName}";
    }
}

internal sealed record TopicSubscription(string Topic, string Subscription)
{
    public string Alias => $"{Topic}/{Subscription}";
}

internal sealed class LiveServiceBusCleanupException : Exception
{
    private LiveServiceBusCleanupException(IReadOnlyCollection<string> failures)
        : base(
            $"Live Azure Service Bus cleanup failed:{Environment.NewLine}- "
            + string.Join($"{Environment.NewLine}- ", failures))
    {
    }

    public static void ThrowIfAny(IReadOnlyCollection<string> failures)
    {
        if (failures.Count > 0)
        {
            throw new LiveServiceBusCleanupException(failures);
        }
    }
}
