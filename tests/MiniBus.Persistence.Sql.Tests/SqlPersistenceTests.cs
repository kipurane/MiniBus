using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using MiniBus.AzureFunctions.DependencyInjection;
using MiniBus.Core.ClaimCheck;
using MiniBus.Core.Contracts;
using MiniBus.Core.Headers;
using MiniBus.Core.Persistence;
using MiniBus.Core.Sagas;
using MiniBus.Core.Serialization;
using MiniBus.Persistence.Sql.DependencyInjection;
using Xunit;

namespace MiniBus.Persistence.Sql.Tests;

public sealed class SqlPersistenceTests
{
    [Fact]
    public void AddMiniBusSqlPersistence_WithConnectionString_ConfiguresSqlConnectionFactory()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddMiniBusSqlPersistence(
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniBusTests;Integrated Security=true;Encrypt=false",
                options =>
                {
                    options.SchemaName = "CustomSchema";
                    options.InboxTableName = "CustomInbox";
                    options.OutboxTableName = "CustomOutbox";
                    options.SagaTableName = "CustomSagas";
                    options.DispatcherBatchSize = 17;
                    options.OutboxClaimLeaseDuration = TimeSpan.FromMinutes(3);
                    options.InboxRetention = TimeSpan.FromDays(30);
                    options.DispatchedOutboxRetention = TimeSpan.FromDays(7);
                    options.FailedOutboxRetention = TimeSpan.FromDays(14);
                    options.CleanupBatchSize = 25;
                })
            .BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<MiniBusSqlPersistenceOptions>();

        Assert.Equal("CustomSchema", options.SchemaName);
        Assert.Equal("CustomInbox", options.InboxTableName);
        Assert.Equal("CustomOutbox", options.OutboxTableName);
        Assert.Equal("CustomSagas", options.SagaTableName);
        Assert.Equal(17, options.DispatcherBatchSize);
        Assert.Equal(TimeSpan.FromMinutes(3), options.OutboxClaimLeaseDuration);
        Assert.Equal(TimeSpan.FromDays(30), options.InboxRetention);
        Assert.Equal(TimeSpan.FromDays(7), options.DispatchedOutboxRetention);
        Assert.Equal(TimeSpan.FromDays(14), options.FailedOutboxRetention);
        Assert.Equal(25, options.CleanupBatchSize);
        Assert.NotNull(options.ConnectionFactory);

        using var connection = Assert.IsType<SqlConnection>(options.ConnectionFactory());
        Assert.Contains("MiniBusTests", connection.ConnectionString, StringComparison.Ordinal);
    }

    [Fact]
    public void AddMiniBusSqlPersistence_UsesExplicitConnectionFactoryWhenConnectionStringIsAlsoConfigured()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddMiniBusSqlPersistence(
                "Server=from-connection-string;Database=MiniBusTests;Encrypt=false",
                options =>
                {
                    options.ConnectionFactory = () => new SqlConnection(
                        "Server=from-factory;Database=MiniBusTests;Encrypt=false");
                })
            .BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<MiniBusSqlPersistenceOptions>();

        Assert.NotNull(options.ConnectionFactory);
        using var connection = Assert.IsType<SqlConnection>(options.ConnectionFactory());
        Assert.Contains("from-factory", connection.ConnectionString, StringComparison.Ordinal);
    }

    [Fact]
    public void AddMiniBusSqlPersistence_WithOptionsConnectionString_ConfiguresSqlConnectionFactory()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddMiniBusSqlPersistence(options =>
            {
                options.ConnectionString = "Server=from-options;Database=MiniBusTests;Encrypt=false";
            })
            .BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<MiniBusSqlPersistenceOptions>();

        Assert.NotNull(options.ConnectionFactory);
        using var connection = Assert.IsType<SqlConnection>(options.ConnectionFactory());
        Assert.Contains("from-options", connection.ConnectionString, StringComparison.Ordinal);
    }

    [Fact]
    public void AddMiniBusSqlPersistence_RegistersSqlSagaPersistence()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddMiniBusSqlPersistence(
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniBusTests;Integrated Security=true;Encrypt=false")
            .BuildServiceProvider();

        Assert.IsType<SqlSagaPersistence>(serviceProvider.GetRequiredService<ISagaPersistence>());
    }

    [Fact]
    public void AddMiniBusSqlPersistence_AfterAzureFunctions_OverridesFallbackSagaPersistence()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddMiniBusAzureFunctions()
            .AddMiniBusSqlPersistence(
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniBusTests;Integrated Security=true;Encrypt=false")
            .BuildServiceProvider();

        Assert.IsType<SqlSagaPersistence>(serviceProvider.GetRequiredService<ISagaPersistence>());
    }

    [Fact]
    public void AddMiniBusSqlPersistence_BeforeAzureFunctions_IsNotOverriddenByFallbackSagaPersistence()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddMiniBusSqlPersistence(
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniBusTests;Integrated Security=true;Encrypt=false")
            .AddMiniBusAzureFunctions()
            .BuildServiceProvider();

        Assert.IsType<SqlSagaPersistence>(serviceProvider.GetRequiredService<ISagaPersistence>());
    }

    [Fact]
    public void AddMiniBusSqlPersistence_DoesNotOverrideExistingCustomSagaPersistence()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddSingleton<ISagaPersistence, CustomSagaPersistence>()
            .AddMiniBusSqlPersistence(
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniBusTests;Integrated Security=true;Encrypt=false")
            .BuildServiceProvider();

        Assert.IsType<CustomSagaPersistence>(serviceProvider.GetRequiredService<ISagaPersistence>());
    }

    [Fact]
    public void AddMiniBusSqlPersistence_DoesNotRegisterHostedDispatcherByDefault()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddSingleton<IMiniBusOutboxDispatcher, RecordingDispatcher>()
            .AddMiniBusSqlPersistence(
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniBusTests;Integrated Security=true;Encrypt=false")
            .BuildServiceProvider();

        Assert.Empty(serviceProvider.GetServices<IHostedService>());
        Assert.NotNull(serviceProvider.GetRequiredService<SqlMiniBusOutboxDispatcher>());
    }

    [Fact]
    public void AddMiniBusSqlHostedOutboxDispatch_RegistersHostedDispatcherAndKeepsManualDispatcher()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddSingleton<IMiniBusOutboxDispatcher, RecordingDispatcher>()
            .AddMiniBusSqlPersistence(
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniBusTests;Integrated Security=true;Encrypt=false")
            .AddMiniBusSqlHostedOutboxDispatch(options =>
            {
                options.PollInterval = TimeSpan.FromMilliseconds(25);
                options.MaxBatchesPerCycle = 3;
                options.FailureBackoff = TimeSpan.FromMilliseconds(50);
                options.DrainOnStartup = false;
            })
            .BuildServiceProvider();

        var settings = serviceProvider.GetRequiredService<MiniBusSqlHostedOutboxDispatchSettings>();

        Assert.Equal(TimeSpan.FromMilliseconds(25), settings.PollInterval);
        Assert.Equal(3, settings.MaxBatchesPerCycle);
        Assert.Equal(TimeSpan.FromMilliseconds(50), settings.FailureBackoff);
        Assert.False(settings.DrainOnStartup);
        Assert.IsType<SqlMiniBusOutboxDispatcher>(serviceProvider.GetRequiredService<SqlMiniBusOutboxDispatcher>());
        Assert.Contains(
            serviceProvider.GetServices<IHostedService>(),
            service => service.GetType() == typeof(SqlMiniBusOutboxHostedDispatcher));
    }

    [Fact]
    public void AddMiniBusSqlHostedOutboxDispatch_DoesNotRegisterMutableOptions()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddSingleton<IMiniBusOutboxDispatcher, RecordingDispatcher>()
            .AddMiniBusSqlPersistence(
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniBusTests;Integrated Security=true;Encrypt=false")
            .AddMiniBusSqlHostedOutboxDispatch(options =>
            {
                options.PollInterval = TimeSpan.FromMilliseconds(25);
                options.MaxBatchesPerCycle = 3;
                options.FailureBackoff = TimeSpan.FromMilliseconds(50);
                options.DrainOnStartup = false;
            })
            .BuildServiceProvider();
        var settings = serviceProvider.GetRequiredService<MiniBusSqlHostedOutboxDispatchSettings>();

        Assert.Null(serviceProvider.GetService<MiniBusSqlHostedOutboxDispatchOptions>());
        Assert.Equal(TimeSpan.FromMilliseconds(25), settings.PollInterval);
        Assert.Equal(3, settings.MaxBatchesPerCycle);
        Assert.Equal(TimeSpan.FromMilliseconds(50), settings.FailureBackoff);
        Assert.False(settings.DrainOnStartup);
    }

    [Theory]
    [InlineData("poll", nameof(MiniBusSqlHostedOutboxDispatchOptions.PollInterval), "poll interval")]
    [InlineData("batches", nameof(MiniBusSqlHostedOutboxDispatchOptions.MaxBatchesPerCycle), "maximum batches per cycle")]
    [InlineData("backoff", nameof(MiniBusSqlHostedOutboxDispatchOptions.FailureBackoff), "failure backoff")]
    public void AddMiniBusSqlHostedOutboxDispatch_ValidatesOptions(
        string invalidOption,
        string expectedParamName,
        string expectedMessage)
    {
        var services = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddSingleton<IMiniBusOutboxDispatcher, RecordingDispatcher>()
            .AddMiniBusSqlPersistence(
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniBusTests;Integrated Security=true;Encrypt=false");

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            services.AddMiniBusSqlHostedOutboxDispatch(options =>
            {
                if (invalidOption == "poll")
                {
                    options.PollInterval = TimeSpan.Zero;
                }

                if (invalidOption == "batches")
                {
                    options.MaxBatchesPerCycle = 0;
                }

                if (invalidOption == "backoff")
                {
                    options.FailureBackoff = TimeSpan.Zero;
                }
            }));

        Assert.Equal(expectedParamName, exception.ParamName);
        Assert.Contains(expectedMessage, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddMiniBusSqlHostedOutboxDispatch_RequiresSqlPersistenceRegistration()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddMiniBusSqlHostedOutboxDispatch());

        Assert.Contains(nameof(MiniBusSqlPersistenceServiceCollectionExtensions.AddMiniBusSqlPersistence), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(MiniBusSqlPersistenceServiceCollectionExtensions.AddMiniBusSqlHostedOutboxDispatch), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddMiniBusSqlHostedOutboxDispatch_DoesNotAcceptManuallyRegisteredSqlServices()
    {
        var services = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddSingleton<ISqlMiniBusOutboxStore, RecordingOutboxStore>()
            .AddSingleton<IMiniBusOutboxDispatcher, RecordingDispatcher>()
            .AddSingleton(new MiniBusSqlPersistenceOptions())
            .AddSingleton<IMiniBusPersistenceSessionFactory>(serviceProvider =>
                new SqlMiniBusPersistenceSessionFactory(
                    serviceProvider.GetRequiredService<MiniBusSqlPersistenceOptions>(),
                    new SqlOutboxOperationSerializer(serviceProvider.GetRequiredService<IMessageSerializer>())))
            .AddSingleton<SqlMiniBusOutboxDispatcher>();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddMiniBusSqlHostedOutboxDispatch());

        Assert.Contains(nameof(MiniBusSqlPersistenceServiceCollectionExtensions.AddMiniBusSqlPersistence), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddMiniBusSqlHostedOutboxDispatch_PreservesCustomDispatchSignal()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddSingleton<IMiniBusOutboxDispatcher, RecordingDispatcher>()
            .AddMiniBusSqlPersistence(
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniBusTests;Integrated Security=true;Encrypt=false")
            .AddSingleton<ISqlMiniBusOutboxDispatchSignal, CustomDispatchSignal>()
            .AddMiniBusSqlHostedOutboxDispatch()
            .BuildServiceProvider();

        Assert.IsType<CustomDispatchSignal>(
            serviceProvider.GetRequiredService<ISqlMiniBusOutboxDispatchSignal>());
    }

    [Fact]
    public void AddMiniBusSqlHostedOutboxDispatch_PreservesCustomDispatchSignalFactory()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddSingleton<IMiniBusOutboxDispatcher, RecordingDispatcher>()
            .AddMiniBusSqlPersistence(
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniBusTests;Integrated Security=true;Encrypt=false")
            .Replace(ServiceDescriptor.Singleton<ISqlMiniBusOutboxDispatchSignal>(_ =>
                new CustomDispatchSignal()))
            .AddMiniBusSqlHostedOutboxDispatch()
            .BuildServiceProvider();

        Assert.IsType<CustomDispatchSignal>(
            serviceProvider.GetRequiredService<ISqlMiniBusOutboxDispatchSignal>());
    }

    [Fact]
    public void AddMiniBusSqlHostedOutboxDispatch_PreservesCustomDispatchSignalFactoryDependencyOnNoopSignal()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddSingleton<IMiniBusOutboxDispatcher, RecordingDispatcher>()
            .AddMiniBusSqlPersistence(
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniBusTests;Integrated Security=true;Encrypt=false")
            .Replace(ServiceDescriptor.Singleton<ISqlMiniBusOutboxDispatchSignal>(serviceProvider =>
                serviceProvider.GetRequiredService<NoopSqlMiniBusOutboxDispatchSignal>()))
            .AddMiniBusSqlHostedOutboxDispatch()
            .BuildServiceProvider();

        Assert.Same(
            serviceProvider.GetRequiredService<NoopSqlMiniBusOutboxDispatchSignal>(),
            serviceProvider.GetRequiredService<ISqlMiniBusOutboxDispatchSignal>());
    }

    [Fact]
    public void AddMiniBusSqlHostedOutboxDispatch_PreservesExplicitBuiltInDispatchSignal()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddSingleton<IMiniBusOutboxDispatcher, RecordingDispatcher>()
            .AddMiniBusSqlPersistence(
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniBusTests;Integrated Security=true;Encrypt=false")
            .Replace(ServiceDescriptor.Singleton<ISqlMiniBusOutboxDispatchSignal, SqlMiniBusOutboxDispatchSignal>())
            .AddMiniBusSqlHostedOutboxDispatch()
            .BuildServiceProvider();

        Assert.IsType<SqlMiniBusOutboxDispatchSignal>(
            serviceProvider.GetRequiredService<ISqlMiniBusOutboxDispatchSignal>());
    }

    [Fact]
    public void AddMiniBusSqlHostedOutboxDispatch_ReplacesDefaultNoopDispatchSignalFactory()
    {
        using var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddSingleton<IMiniBusOutboxDispatcher, RecordingDispatcher>()
            .AddMiniBusSqlPersistence(
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniBusTests;Integrated Security=true;Encrypt=false")
            .AddMiniBusSqlHostedOutboxDispatch()
            .BuildServiceProvider();

        Assert.IsType<SqlMiniBusOutboxDispatchSignal>(
            serviceProvider.GetRequiredService<ISqlMiniBusOutboxDispatchSignal>());
        Assert.Empty(serviceProvider.GetServices<NoopSqlMiniBusOutboxDispatchSignal>());
    }

    [Fact]
    public void AddMiniBusSqlHostedOutboxDispatch_PreservesExplicitNoopDispatchSignalInstance()
    {
        var noopSignal = new NoopSqlMiniBusOutboxDispatchSignal();
        var services = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddSingleton<IMiniBusOutboxDispatcher, RecordingDispatcher>()
            .AddMiniBusSqlPersistence(
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniBusTests;Integrated Security=true;Encrypt=false");

        services.Replace(ServiceDescriptor.Singleton<ISqlMiniBusOutboxDispatchSignal>(
            noopSignal));

        using var serviceProvider = services
            .AddMiniBusSqlHostedOutboxDispatch()
            .BuildServiceProvider();

        Assert.Same(noopSignal, serviceProvider.GetRequiredService<ISqlMiniBusOutboxDispatchSignal>());
    }

    [Fact]
    public void AddMiniBusSqlHostedOutboxDispatch_PreservesExplicitNoopDispatchSignalType()
    {
        var services = new ServiceCollection()
            .AddSingleton<IMessageSerializer, RecordingSerializer>()
            .AddSingleton<IMiniBusOutboxDispatcher, RecordingDispatcher>()
            .AddMiniBusSqlPersistence(
                "Server=(localdb)\\MSSQLLocalDB;Database=MiniBusTests;Integrated Security=true;Encrypt=false");

        services.Replace(ServiceDescriptor.Singleton<ISqlMiniBusOutboxDispatchSignal, NoopSqlMiniBusOutboxDispatchSignal>());

        using var serviceProvider = services
            .AddMiniBusSqlHostedOutboxDispatch()
            .BuildServiceProvider();

        Assert.IsType<NoopSqlMiniBusOutboxDispatchSignal>(
            serviceProvider.GetRequiredService<ISqlMiniBusOutboxDispatchSignal>());
    }

    [Fact]
    public void OutboxOperationSerializer_PreservesMessageBodyMetadataHeadersAndDueTime()
    {
        var serializer = new SqlOutboxOperationSerializer(new RecordingSerializer());
        var dueTime = DateTimeOffset.UtcNow.AddMinutes(5);
        var operation = new MiniBusOutboxOperation(
            MiniBusOutboxOperationKind.Schedule,
            new TestCommand(Guid.NewGuid()),
            typeof(TestCommand),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MiniBusHeaderNames.CorrelationId] = "correlation-1",
                [MiniBusHeaderNames.CausationId] = "message-1"
            },
            dueTime);

        var serialized = serializer.Serialize(operation);
        var stored = serializer.Deserialize(
            Guid.NewGuid(),
            "outgoing-message-1",
            serialized.OperationKind,
            serialized.MessageType,
            serialized.Body,
            serialized.HeadersJson,
            serialized.DueTime,
            attemptCount: 3);

        Assert.Equal(MiniBusOutboxOperationKind.Schedule, stored.Kind);
        Assert.Equal("outgoing-message-1", stored.OutgoingMessageId);
        Assert.Equal(typeof(TestCommand), stored.MessageType);
        Assert.Equal("serialized:TestCommand", stored.Body.ToString());
        Assert.Equal("correlation-1", stored.Headers[MiniBusHeaderNames.CorrelationId]);
        Assert.Equal("message-1", stored.Headers[MiniBusHeaderNames.CausationId]);
        Assert.Equal(dueTime, stored.DueTime);
        Assert.Equal(3, stored.AttemptCount);
    }

    [Fact]
    public void OutboxOperationSerializer_PreservesSagaTimeoutSchedule()
    {
        var serializer = new SqlOutboxOperationSerializer(new RecordingSerializer());
        var dueTime = DateTimeOffset.UtcNow.AddMinutes(5);
        var operation = new MiniBusOutboxOperation(
            MiniBusOutboxOperationKind.Schedule,
            new TestTimeout("saga-1"),
            typeof(TestTimeout),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MiniBusHeaderNames.CorrelationId] = "correlation-1",
                [MiniBusHeaderNames.CausationId] = "message-1"
            },
            dueTime);

        var serialized = serializer.Serialize(operation);
        var stored = serializer.Deserialize(
            Guid.NewGuid(),
            "outgoing-timeout-1",
            serialized.OperationKind,
            serialized.MessageType,
            serialized.Body,
            serialized.HeadersJson,
            serialized.DueTime,
            attemptCount: 1);

        Assert.Equal(MiniBusOutboxOperationKind.Schedule, stored.Kind);
        Assert.Equal(typeof(TestTimeout), stored.MessageType);
        Assert.Equal("serialized:TestTimeout", stored.Body.ToString());
        Assert.Equal("correlation-1", stored.Headers[MiniBusHeaderNames.CorrelationId]);
        Assert.Equal("message-1", stored.Headers[MiniBusHeaderNames.CausationId]);
        Assert.Equal(dueTime, stored.DueTime);
    }

    [Fact]
    public async Task OutboxOperationSerializer_StoresClaimCheckBodyAndHeadersForLargePayload()
    {
        var store = new RecordingClaimCheckPayloadStore();
        var serializer = new SqlOutboxOperationSerializer(
            new RecordingSerializer(),
            new MiniBusClaimCheckOptions { Enabled = true, PayloadThresholdBytes = 3 },
            store);
        var operation = new MiniBusOutboxOperation(
            MiniBusOutboxOperationKind.Send,
            new TestCommand(Guid.NewGuid()),
            typeof(TestCommand),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [MiniBusHeaderNames.CorrelationId] = "correlation-1",
                [MiniBusHeaderNames.CausationId] = "message-1"
            },
            DueTime: null);

        var serialized = await serializer.SerializeAsync(operation);
        var stored = serializer.Deserialize(
            Guid.NewGuid(),
            "outgoing-message-1",
            serialized.OperationKind,
            serialized.MessageType,
            serialized.Body,
            serialized.HeadersJson,
            serialized.DueTime,
            attemptCount: 1);

        Assert.Equal("serialized:TestCommand", Assert.Single(store.Writes).Payload.ToString());
        Assert.NotEqual("serialized:TestCommand", stored.Body.ToString());
        Assert.Equal(bool.TrueString, stored.Headers[MiniBusClaimCheckHeaderNames.Enabled]);
        Assert.Equal(MiniBusClaimCheckProviderNames.AzureBlobStorage, stored.Headers[MiniBusClaimCheckHeaderNames.Provider]);
        Assert.Equal("payloads/payload-1.bin", stored.Headers[MiniBusClaimCheckHeaderNames.BlobName]);
        Assert.Equal("correlation-1", stored.Headers[MiniBusHeaderNames.CorrelationId]);
        Assert.Equal("message-1", stored.Headers[MiniBusHeaderNames.CausationId]);
    }

    [Fact]
    public void OutboxOperationSerializer_SyncSerializeRejectsEnabledClaimCheck()
    {
        var serializer = new SqlOutboxOperationSerializer(
            new RecordingSerializer(),
            new MiniBusClaimCheckOptions { Enabled = true, PayloadThresholdBytes = 3 },
            new RecordingClaimCheckPayloadStore());
        var operation = new MiniBusOutboxOperation(
            MiniBusOutboxOperationKind.Send,
            new TestCommand(Guid.NewGuid()),
            typeof(TestCommand),
            new Dictionary<string, string>(StringComparer.Ordinal),
            DueTime: null);

        var exception = Assert.Throws<InvalidOperationException>(() => serializer.Serialize(operation));

        Assert.Contains(nameof(SqlOutboxOperationSerializer.SerializeAsync), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OutboxDispatcher_MarksSuccessfulDispatch()
    {
        var operation = CreateStoredOperation();
        var store = new RecordingOutboxStore(operation);
        var dispatcher = new RecordingDispatcher();
        var sqlDispatcher = new SqlMiniBusOutboxDispatcher(
            store,
            dispatcher,
            new MiniBusSqlPersistenceOptions { DispatcherBatchSize = 10 });

        var dispatched = await sqlDispatcher.DispatchPendingAsync();

        Assert.Equal(1, dispatched);
        Assert.Equal(10, store.ClaimedBatchSize);
        Assert.Single(dispatcher.Dispatched);
        Assert.Contains(operation.Id, store.MarkedDispatched);
        Assert.Empty(store.MarkedFailed);
    }

    [Fact]
    public async Task OutboxDispatcher_DispatchPendingAsync_DispatchesSingleBatchByDefault()
    {
        var firstOperation = CreateStoredOperation();
        var secondOperation = CreateStoredOperation();
        var store = new SequencedOutboxStore(
            new[] { firstOperation },
            new[] { secondOperation },
            Array.Empty<MiniBusOutboxStoredOperation>());
        var dispatcher = new RecordingDispatcher();
        var sqlDispatcher = new SqlMiniBusOutboxDispatcher(
            store,
            dispatcher,
            new MiniBusSqlPersistenceOptions { DispatcherBatchSize = 1 });

        var dispatched = await sqlDispatcher.DispatchPendingAsync();

        Assert.Equal(1, dispatched);
        Assert.Equal(1, store.ClaimCount);
        Assert.Single(dispatcher.Dispatched);
    }

    [Fact]
    public async Task OutboxDispatcher_DispatchPendingBatchesAsync_DispatchesUpToMaxBatches()
    {
        var firstOperation = CreateStoredOperation();
        var secondOperation = CreateStoredOperation();
        var store = new SequencedOutboxStore(
            new[] { firstOperation },
            new[] { secondOperation },
            new[] { CreateStoredOperation() });
        var dispatcher = new RecordingDispatcher();
        var sqlDispatcher = new SqlMiniBusOutboxDispatcher(
            store,
            dispatcher,
            new MiniBusSqlPersistenceOptions { DispatcherBatchSize = 1 });

        var dispatched = await sqlDispatcher.DispatchPendingBatchesAsync(maxBatches: 2);

        Assert.Equal(2, dispatched);
        Assert.Equal(2, store.ClaimCount);
        Assert.Equal(2, dispatcher.Dispatched.Count);
    }

    [Fact]
    public async Task OutboxDispatcher_DispatchPendingBatchesAsync_ValidatesMaxBatches()
    {
        var sqlDispatcher = new SqlMiniBusOutboxDispatcher(
            new SequencedOutboxStore(Array.Empty<MiniBusOutboxStoredOperation>()),
            new RecordingDispatcher(),
            new MiniBusSqlPersistenceOptions { DispatcherBatchSize = 1 });

        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sqlDispatcher.DispatchPendingBatchesAsync(maxBatches: 0));

        Assert.Equal("maxBatches", exception.ParamName);
        Assert.Contains("maximum number of SQL outbox dispatch batches", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OutboxDispatcher_DispatchPendingAsync_ThrowsWhenCancellationAlreadyRequested()
    {
        var sqlDispatcher = new SqlMiniBusOutboxDispatcher(
            new SequencedOutboxStore(Array.Empty<MiniBusOutboxStoredOperation>()),
            new RecordingDispatcher(),
            new MiniBusSqlPersistenceOptions { DispatcherBatchSize = 1 });
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sqlDispatcher.DispatchPendingAsync(cancellation.Token));
    }

    [Fact]
    public async Task OutboxDispatcher_ReturnsClaimAndFailureCountsForDispatchCycle()
    {
        var operation = CreateStoredOperation();
        var store = new RecordingOutboxStore(operation);
        var dispatcher = new RecordingDispatcher
        {
            Exception = new InvalidOperationException("transport failed")
        };
        var sqlDispatcher = new SqlMiniBusOutboxDispatcher(
            store,
            dispatcher,
            new MiniBusSqlPersistenceOptions { DispatcherBatchSize = 5 });

        var result = await sqlDispatcher.DispatchPendingBatchAsync();

        Assert.Equal(1, result.ClaimedCount);
        Assert.Equal(0, result.DispatchedCount);
        Assert.Equal(1, result.FailedCount);
    }

    [Fact]
    public async Task HostedOutboxDispatcher_DispatchCycle_DrainsMultipleBatches()
    {
        var firstOperation = CreateStoredOperation();
        var secondOperation = CreateStoredOperation();
        var store = new SequencedOutboxStore(
            new[] { firstOperation },
            new[] { secondOperation },
            Array.Empty<MiniBusOutboxStoredOperation>());
        var dispatcher = new RecordingDispatcher();
        var hostedDispatcher = CreateHostedDispatcher(store, dispatcher, options =>
        {
            options.MaxBatchesPerCycle = 5;
        });

        var result = await hostedDispatcher.DispatchCycleAsync();

        Assert.Equal(3, result.BatchAttemptCount);
        Assert.Equal(2, result.ClaimedCount);
        Assert.Equal(2, result.DispatchedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.False(result.BackoffRequired);
        Assert.Equal(3, store.ClaimCount);
        Assert.Equal(2, dispatcher.Dispatched.Count);
    }

    [Fact]
    public async Task HostedOutboxDispatcher_DispatchCycle_StopsAndBacksOffAfterClaimedFailure()
    {
        var operation = CreateStoredOperation();
        var store = new SequencedOutboxStore(
            new[] { operation },
            new[] { CreateStoredOperation() });
        var dispatcher = new RecordingDispatcher
        {
            Exception = new InvalidOperationException("transport failed")
        };
        var hostedDispatcher = CreateHostedDispatcher(store, dispatcher, options =>
        {
            options.MaxBatchesPerCycle = 5;
        });

        var result = await hostedDispatcher.DispatchCycleAsync();

        Assert.Equal(1, result.BatchAttemptCount);
        Assert.Equal(1, result.ClaimedCount);
        Assert.Equal(0, result.DispatchedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.True(result.BackoffRequired);
        Assert.Equal(1, store.ClaimCount);
    }

    [Theory]
    [InlineData("dispatcher")]
    [InlineData("settings")]
    [InlineData("signal")]
    public void HostedOutboxDispatcher_Constructor_ValidatesRequiredDependencies(string nullDependency)
    {
        var dispatcher = new SqlMiniBusOutboxDispatcher(
            new SequencedOutboxStore(Array.Empty<MiniBusOutboxStoredOperation>()),
            new RecordingDispatcher(),
            new MiniBusSqlPersistenceOptions { DispatcherBatchSize = 1 });
        var settings = new MiniBusSqlHostedOutboxDispatchOptions().ToSettings();
        var signal = new SqlMiniBusOutboxDispatchSignal();

        var exception = Assert.Throws<ArgumentNullException>(() =>
            new SqlMiniBusOutboxHostedDispatcher(
                nullDependency == "dispatcher" ? null! : dispatcher,
                nullDependency == "settings" ? null! : settings,
                nullDependency == "signal" ? null! : signal,
                NullLogger<SqlMiniBusOutboxHostedDispatcher>.Instance));

        Assert.Equal(nullDependency, exception.ParamName);
    }

    [Fact]
    public async Task HostedOutboxDispatcher_ExecuteAsync_ContinuesImmediatelyWhenBacklogMayRemain()
    {
        var store = new SequencedOutboxStore(
            new[] { CreateStoredOperation() },
            new[] { CreateStoredOperation() },
            Array.Empty<MiniBusOutboxStoredOperation>());
        var dispatcher = new RecordingDispatcher();
        var signal = new CoordinatedDispatchSignal();
        var hostedDispatcher = CreateHostedDispatcher(
            store,
            dispatcher,
            options =>
            {
                options.MaxBatchesPerCycle = 1;
                options.PollInterval = TimeSpan.FromSeconds(30);
                options.FailureBackoff = TimeSpan.FromSeconds(30);
                options.DrainOnStartup = true;
            },
            signal);

        await hostedDispatcher.StartAsync(CancellationToken.None);
        await signal.WaitEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await hostedDispatcher.StopAsync(timeout.Token);

        Assert.Equal(3, store.ClaimCount);
        Assert.Equal(2, dispatcher.Dispatched.Count);
    }

    [Fact]
    public async Task HostedOutboxDispatcher_StopAsync_CancelsIdlePollingPromptly()
    {
        var store = new SequencedOutboxStore(Array.Empty<MiniBusOutboxStoredOperation>());
        var signal = new CoordinatedDispatchSignal();
        var hostedDispatcher = CreateHostedDispatcher(
            store,
            new RecordingDispatcher(),
            options =>
            {
                options.DrainOnStartup = false;
                options.PollInterval = TimeSpan.FromSeconds(30);
                options.FailureBackoff = TimeSpan.FromSeconds(30);
            },
            signal);

        await hostedDispatcher.StartAsync(CancellationToken.None);
        await signal.WaitEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await hostedDispatcher.StopAsync(timeout.Token);

        Assert.True(signal.CancellationObserved);
        Assert.Equal(0, store.ClaimCount);
    }

    [Fact]
    public async Task HostedOutboxDispatcher_ExecuteAsync_FallsBackToPollCycleWhenSignalWaitFails()
    {
        var store = new SequencedOutboxStore(
            new[] { CreateStoredOperation() },
            Array.Empty<MiniBusOutboxStoredOperation>());
        var dispatcher = new RecordingDispatcher();
        var signal = new FailingThenCoordinatedDispatchSignal();
        var hostedDispatcher = CreateHostedDispatcher(
            store,
            dispatcher,
            options =>
            {
                options.DrainOnStartup = false;
                options.PollInterval = TimeSpan.FromSeconds(30);
                options.FailureBackoff = TimeSpan.FromMilliseconds(10);
            },
            signal);

        await hostedDispatcher.StartAsync(CancellationToken.None);
        await signal.SecondWaitEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await hostedDispatcher.StopAsync(timeout.Token);

        Assert.Equal(2, signal.WaitCount);
        Assert.True(signal.CancellationObserved);
        Assert.Equal(2, store.ClaimCount);
        Assert.Single(dispatcher.Dispatched);
    }

    [Fact]
    public async Task OutboxDispatchSignal_WaitAsync_PropagatesCancellation()
    {
        var signal = new SqlMiniBusOutboxDispatchSignal();
        using var cancellation = new CancellationTokenSource();
        var wait = signal.WaitAsync(TimeSpan.FromSeconds(30), cancellation.Token).AsTask();

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => wait);
    }

    [Fact]
    public async Task OutboxDispatchSignal_WaitAsync_ReturnsFalseOnTimeout()
    {
        var signal = new SqlMiniBusOutboxDispatchSignal();

        var wasWoken = await signal.WaitAsync(TimeSpan.FromMilliseconds(1), CancellationToken.None);

        Assert.False(wasWoken);
    }

    [Fact]
    public async Task OutboxDispatcher_RecordsFailureAndLeavesOperationRetryable()
    {
        var operation = CreateStoredOperation();
        var store = new RecordingOutboxStore(operation);
        var dispatcher = new RecordingDispatcher
        {
            Exception = new InvalidOperationException("transport failed")
        };
        var sqlDispatcher = new SqlMiniBusOutboxDispatcher(
            store,
            dispatcher,
            new MiniBusSqlPersistenceOptions { DispatcherBatchSize = 5 });

        var dispatched = await sqlDispatcher.DispatchPendingAsync();

        Assert.Equal(0, dispatched);
        Assert.Equal(5, store.ClaimedBatchSize);
        var failure = Assert.Single(store.MarkedFailed);
        Assert.Equal(operation.Id, failure.OperationId);
        Assert.Contains("transport failed", failure.Exception.Message, StringComparison.Ordinal);
        Assert.Empty(store.MarkedDispatched);
    }

    [Fact]
    public void SchemaScript_DefinesInboxAndOutboxOperationalColumns()
    {
        var script = File.ReadAllText(Path.GetFullPath(
            "../../../../../src/MiniBus.Persistence.Sql/Schema/001-inbox-outbox.sql",
            AppContext.BaseDirectory));

        Assert.Contains("CREATE TABLE MiniBus.Inbox", script, StringComparison.Ordinal);
        Assert.Contains("EndpointName", script, StringComparison.Ordinal);
        Assert.Contains("MessageId", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE MiniBus.Outbox", script, StringComparison.Ordinal);
        Assert.Contains("OperationKind", script, StringComparison.Ordinal);
        Assert.Contains("OutgoingMessageId", script, StringComparison.Ordinal);
        Assert.Contains("HeadersJson", script, StringComparison.Ordinal);
        Assert.Contains("AttemptCount", script, StringComparison.Ordinal);
        Assert.Contains("DispatchedUtc", script, StringComparison.Ordinal);
    }

    [Fact]
    public void AdditiveSchemaScript_AddsOutgoingMessageIdForExistingOutboxTables()
    {
        var script = File.ReadAllText(Path.GetFullPath(
            "../../../../../src/MiniBus.Persistence.Sql/Schema/002-outbox-outgoing-message-id.sql",
            AppContext.BaseDirectory));

        Assert.Contains("COL_LENGTH", script, StringComparison.Ordinal);
        Assert.Contains("OutgoingMessageId", script, StringComparison.Ordinal);
        Assert.Contains("UX_MiniBus_Outbox_OutgoingMessageId", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SagaSchemaScript_DefinesSagaOperationalColumns()
    {
        var script = File.ReadAllText(Path.GetFullPath(
            "../../../../../src/MiniBus.Persistence.Sql/Schema/003-sagas.sql",
            AppContext.BaseDirectory));

        Assert.Contains("CREATE TABLE MiniBus.Sagas", script, StringComparison.Ordinal);
        Assert.Contains("DataType", script, StringComparison.Ordinal);
        Assert.Contains("CorrelationId", script, StringComparison.Ordinal);
        Assert.Contains("Data varbinary(max)", script, StringComparison.Ordinal);
        Assert.Contains("IsCompleted", script, StringComparison.Ordinal);
        Assert.Contains("CompletedUtc", script, StringComparison.Ordinal);
        Assert.Contains("Version rowversion", script, StringComparison.Ordinal);
        Assert.Contains("UX_MiniBus_Sagas_DataType_CorrelationId", script, StringComparison.Ordinal);
    }

    private static MiniBusOutboxStoredOperation CreateStoredOperation()
    {
        return new MiniBusOutboxStoredOperation(
            Guid.NewGuid(),
            "outgoing-message-1",
            MiniBusOutboxOperationKind.Send,
            BinaryData.FromString("{}"),
            typeof(TestCommand),
            new Dictionary<string, string>(StringComparer.Ordinal),
            DueTime: null,
            AttemptCount: 0);
    }

    private static SqlMiniBusOutboxHostedDispatcher CreateHostedDispatcher(
        ISqlMiniBusOutboxStore store,
        IMiniBusOutboxDispatcher dispatcher,
        Action<MiniBusSqlHostedOutboxDispatchOptions>? configureOptions = null,
        ISqlMiniBusOutboxDispatchSignal? signal = null)
    {
        var options = new MiniBusSqlHostedOutboxDispatchOptions
        {
            PollInterval = TimeSpan.FromMilliseconds(10),
            FailureBackoff = TimeSpan.FromMilliseconds(10),
            MaxBatchesPerCycle = 10,
            DrainOnStartup = true
        };
        configureOptions?.Invoke(options);

        return new SqlMiniBusOutboxHostedDispatcher(
            new SqlMiniBusOutboxDispatcher(
                store,
                dispatcher,
                new MiniBusSqlPersistenceOptions { DispatcherBatchSize = 10 }),
            options.ToSettings(),
            signal ?? new SqlMiniBusOutboxDispatchSignal(),
            NullLogger<SqlMiniBusOutboxHostedDispatcher>.Instance);
    }

    private sealed record TestCommand(Guid Id) : ICommand;

    private sealed record TestTimeout(string CorrelationId) : ISagaTimeout;

    private sealed class CustomSagaPersistence : ISagaPersistence
    {
        public Task<SagaPersistenceRecord<TData>?> LoadAsync<TData>(
            string correlationId,
            CancellationToken cancellationToken = default)
            where TData : class, ISagaData, new()
        {
            throw new NotSupportedException();
        }

        public Task CreateAsync<TData>(
            TData data,
            CancellationToken cancellationToken = default)
            where TData : class, ISagaData, new()
        {
            throw new NotSupportedException();
        }

        public Task SaveAsync<TData>(
            TData data,
            string? version,
            CancellationToken cancellationToken = default)
            where TData : class, ISagaData, new()
        {
            throw new NotSupportedException();
        }

        public Task CompleteAsync<TData>(
            TData data,
            string? version,
            CancellationToken cancellationToken = default)
            where TData : class, ISagaData, new()
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingSerializer : IMessageSerializer
    {
        public BinaryData Serialize(object message, Type messageType)
        {
            return BinaryData.FromString($"serialized:{messageType.Name}");
        }

        public object Deserialize(BinaryData body, Type messageType)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingOutboxStore : ISqlMiniBusOutboxStore
    {
        private readonly IReadOnlyList<MiniBusOutboxStoredOperation> _operations;

        public RecordingOutboxStore(params MiniBusOutboxStoredOperation[] operations)
        {
            _operations = operations;
        }

        public int? ClaimedBatchSize { get; private set; }

        public List<Guid> MarkedDispatched { get; } = new();

        public List<(Guid OperationId, Exception Exception)> MarkedFailed { get; } = new();

        public Task<IReadOnlyList<MiniBusOutboxStoredOperation>> ClaimPendingAsync(
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            ClaimedBatchSize = batchSize;
            return Task.FromResult<IReadOnlyList<MiniBusOutboxStoredOperation>>(
                _operations
                    .Where(operation => !MarkedDispatched.Contains(operation.Id))
                    .ToArray());
        }

        public Task MarkDispatchedAsync(Guid operationId, CancellationToken cancellationToken = default)
        {
            MarkedDispatched.Add(operationId);
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            Guid operationId,
            Exception exception,
            CancellationToken cancellationToken = default)
        {
            MarkedFailed.Add((operationId, exception));
            return Task.CompletedTask;
        }

        public Task<int> CleanupAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }
    }

    private sealed class SequencedOutboxStore : ISqlMiniBusOutboxStore
    {
        private readonly Queue<IReadOnlyList<MiniBusOutboxStoredOperation>> _batches;

        public SequencedOutboxStore(params IReadOnlyList<MiniBusOutboxStoredOperation>[] batches)
        {
            _batches = new Queue<IReadOnlyList<MiniBusOutboxStoredOperation>>(batches);
        }

        public int ClaimCount { get; private set; }

        public List<Guid> MarkedDispatched { get; } = new();

        public List<(Guid OperationId, Exception Exception)> MarkedFailed { get; } = new();

        public Task<IReadOnlyList<MiniBusOutboxStoredOperation>> ClaimPendingAsync(
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            ClaimCount++;

            if (_batches.Count == 0)
            {
                return Task.FromResult<IReadOnlyList<MiniBusOutboxStoredOperation>>(
                    Array.Empty<MiniBusOutboxStoredOperation>());
            }

            return Task.FromResult(_batches.Dequeue());
        }

        public Task MarkDispatchedAsync(Guid operationId, CancellationToken cancellationToken = default)
        {
            MarkedDispatched.Add(operationId);
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            Guid operationId,
            Exception exception,
            CancellationToken cancellationToken = default)
        {
            MarkedFailed.Add((operationId, exception));
            return Task.CompletedTask;
        }

        public Task<int> CleanupAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }
    }

    private sealed class RecordingClaimCheckPayloadStore : IMiniBusClaimCheckPayloadStore
    {
        public List<(BinaryData Payload, MiniBusClaimCheckPayloadWriteOptions? Options)> Writes { get; } = new();

        public Task<MiniBusClaimCheckPayloadReference> WriteAsync(
            BinaryData payload,
            MiniBusClaimCheckPayloadWriteOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Writes.Add((payload, options));
            return Task.FromResult(new MiniBusClaimCheckPayloadReference(
                MiniBusClaimCheckProviderNames.AzureBlobStorage,
                "minibus-payloads",
                "payloads/payload-1.bin",
                "payload-1",
                payload.ToArray().LongLength,
                options?.ContentType,
                new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero),
                null));
        }

        public Task<BinaryData> ReadAsync(
            MiniBusClaimCheckPayloadReference reference,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingDispatcher : IMiniBusOutboxDispatcher
    {
        public Exception? Exception { get; init; }

        public List<MiniBusOutboxStoredOperation> Dispatched { get; } = new();

        public Task DispatchAsync(
            MiniBusOutboxStoredOperation operation,
            CancellationToken cancellationToken = default)
        {
            if (Exception is not null)
            {
                return Task.FromException(Exception);
            }

            Dispatched.Add(operation);
            return Task.CompletedTask;
        }
    }

    private sealed class CustomDispatchSignal : ISqlMiniBusOutboxDispatchSignal
    {
        public void Wake()
        {
        }

        public ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(false);
        }
    }

    private sealed class CoordinatedDispatchSignal : ISqlMiniBusOutboxDispatchSignal
    {
        public TaskCompletionSource WaitEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool CancellationObserved { get; private set; }

        public void Wake()
        {
        }

        public async ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            WaitEntered.TrySetResult();

            try
            {
                await Task.Delay(timeout, cancellationToken);
                return false;
            }
            catch (OperationCanceledException)
            {
                CancellationObserved = true;
                throw;
            }
        }
    }

    private sealed class FailingThenCoordinatedDispatchSignal : ISqlMiniBusOutboxDispatchSignal
    {
        public TaskCompletionSource SecondWaitEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int WaitCount { get; private set; }

        public bool CancellationObserved { get; private set; }

        public void Wake()
        {
        }

        public async ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            WaitCount++;

            if (WaitCount == 1)
            {
                throw new InvalidOperationException("signal failed");
            }

            SecondWaitEntered.TrySetResult();

            try
            {
                await Task.Delay(timeout, cancellationToken);
                return false;
            }
            catch (OperationCanceledException)
            {
                CancellationObserved = true;
                throw;
            }
        }
    }
}
