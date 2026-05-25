extern alias DispatcherApp;

using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MiniBus.AzureServiceBus.Dispatching;
using MiniBus.Persistence.Sql;
using MiniBus.Samples.Billing.FunctionApp;
using BillingOutboxDispatcherFunction = DispatcherApp::MiniBus.Samples.Billing.OutboxDispatcher.FunctionApp.BillingOutboxDispatcherFunction;
using DispatcherProgram = DispatcherApp::MiniBus.Samples.Billing.OutboxDispatcher.FunctionApp.Program;

namespace MiniBus.AcceptanceTests;

public sealed class BillingOutboxDispatcherFunctionAppTests
{
    [Fact]
    public void ConfigureServices_ComposesSqlOutboxDispatcherForTimerFunction()
    {
        var configuration = CreateDispatcherConfiguration();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(configuration);
        DispatcherProgram.ConfigureServices(services, configuration);
        services.RemoveAll<IAzureServiceBusSender>();
        services.AddSingleton<IAzureServiceBusSender>(new ReferenceSolutionAcceptanceTests.RecordingServiceBusSender());

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        Assert.NotNull(provider.GetRequiredService<SqlMiniBusOutboxDispatcher>());
        Assert.NotNull(ActivatorUtilities.CreateInstance<BillingOutboxDispatcherFunction>(provider));
    }

    [Fact]
    public async Task Run_ForwardsBoundedBatchCountCancellationAndPastDueLogMetadata()
    {
        var observedMaxBatches = 0;
        var observedCancellationToken = CancellationToken.None;
        var logger = new RecordingLogger();
        var function = new BillingOutboxDispatcherFunction(
            (maxBatches, cancellationToken) =>
            {
                observedMaxBatches = maxBatches;
                observedCancellationToken = cancellationToken;
                return Task.FromResult(11);
            },
            maxBatches: 4,
            logger);
        using var cancellationTokenSource = new CancellationTokenSource();

        await function.Run(
            new TimerInfo
            {
                IsPastDue = true
            },
            cancellationTokenSource.Token);

        Assert.Equal(4, observedMaxBatches);
        Assert.Equal(cancellationTokenSource.Token, observedCancellationToken);
        var log = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.Equal(BillingOutboxDispatcherFunction.OutboxDrainCompletedEventId, log.EventId.Id);
        Assert.Equal(11, log.State["dispatchedOperationCount"]);
        Assert.Equal(4, log.State["maxBatches"]);
        Assert.Equal(true, log.State["isPastDue"]);
    }

    [Fact]
    public async Task DispatchPendingAsync_UsesConfiguredBoundedBatchCount()
    {
        var observedMaxBatches = 0;
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var function = new BillingOutboxDispatcherFunction(
            (maxBatches, cancellationToken) =>
            {
                observedMaxBatches = maxBatches;
                Assert.False(cancellationToken.IsCancellationRequested);
                return Task.FromResult(7);
            },
            maxBatches: 3,
            loggerFactory.CreateLogger<BillingOutboxDispatcherFunction>());

        var dispatched = await function.DispatchPendingAsync();

        Assert.Equal(3, observedMaxBatches);
        Assert.Equal(7, dispatched);
    }

    [Theory]
    [InlineData(null, BillingOutboxDispatcherFunction.DefaultMaxBatches)]
    [InlineData("9", 9)]
    public void GetMaxBatches_ReadsOptionalPositiveConfiguration(
        string? configuredValue,
        int expectedMaxBatches)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [BillingOutboxDispatcherFunction.MaxBatchesSetting] = configuredValue
            })
            .Build();

        Assert.Equal(
            expectedMaxBatches,
            BillingOutboxDispatcherFunction.GetMaxBatches(configuration));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-number")]
    public void GetMaxBatches_RejectsInvalidConfiguration(string configuredValue)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [BillingOutboxDispatcherFunction.MaxBatchesSetting] = configuredValue
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(
            () => BillingOutboxDispatcherFunction.GetMaxBatches(configuration));
        Assert.Contains(BillingOutboxDispatcherFunction.MaxBatchesSetting, exception.Message, StringComparison.Ordinal);
        Assert.Contains($"'{configuredValue}'", exception.Message, StringComparison.Ordinal);
    }

    private static IConfiguration CreateDispatcherConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [BillingTopology.ServiceBusConnectionSetting] = BillingTopology.EmulatorConnectionString,
                [BillingSampleSqlPersistence.EnabledSetting] = bool.TrueString,
                [BillingSampleSqlPersistence.ConnectionSetting] = BillingSampleSqlPersistence.LocalConnectionString,
                [BillingOutboxDispatcherFunction.MaxBatchesSetting] = "2",
                [BillingOutboxDispatcherFunction.ScheduleSetting] = "*/15 * * * * *"
            })
            .Build();
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var values = state as IReadOnlyList<KeyValuePair<string, object?>>
                         ?? [];
            var stateDictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (value.Key != "{OriginalFormat}")
                {
                    stateDictionary[value.Key] = value.Value;
                }
            }

            Entries.Add(new LogEntry(
                logLevel,
                eventId,
                stateDictionary));
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        EventId EventId,
        IReadOnlyDictionary<string, object?> State);

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
