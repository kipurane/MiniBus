using Azure.Messaging.ServiceBus;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniBus.AzureFunctions.Processing;
using MiniBus.AzureFunctions.Settlement;
using MiniBus.AzureServiceBus.Dispatching;
using MiniBus.Core.Headers;
using MiniBus.Core.Persistence;
using MiniBus.Core.Sagas;
using MiniBus.Core.Serialization;
using MiniBus.Persistence.Sql.DependencyInjection;
using MiniBus.Samples.FunctionApp;
using MiniBus.Samples.FunctionApp.Contracts;
using MiniBus.Samples.FunctionApp.Sagas;

namespace MiniBus.AcceptanceTests;

public sealed class ReferenceSolutionAcceptanceTests
{
    [Fact]
    public async Task Tier1_SampleStyleBillingWorkflow_ComposesWithoutInfrastructure()
    {
        var sender = new RecordingServiceBusSender();
        using var provider = BuildSampleStyleProvider(sender);
        var processor = provider.GetRequiredService<MiniBusProcessor>();
        var invoiceId = "invoice-1";
        var customerId = "customer-1";
        var commandActions = new RecordingMessageActions();

        await processor.ProcessAsync(
            CreateReceivedMessage(
                new CreateInvoice(invoiceId, customerId, 123.45m),
                messageId: "command-message-1",
                correlationId: "billing-correlation-1"),
            commandActions);

        Assert.NotNull(commandActions.CompletedMessage);
        Assert.Null(commandActions.DeadLetteredMessage);
        Assert.Contains(sender.Sends, send =>
            send.Destination == "billing-receipts"
            && HasMessageType<SendInvoiceReceipt>(send.Message));
        var invoiceCreatedSend = Assert.Single(sender.Sends, send =>
            send.Destination == "domain-events"
            && HasMessageType<InvoiceCreated>(send.Message));

        var eventActions = new RecordingMessageActions();
        await processor.ProcessAsync(ToReceivedMessage(invoiceCreatedSend.Message), eventActions);

        Assert.NotNull(eventActions.CompletedMessage);
        Assert.Null(eventActions.DeadLetteredMessage);
        var timeoutSchedule = Assert.Single(sender.Schedules, schedule =>
            schedule.Destination == "billing-timeouts"
            && HasMessageType<InvoicePaymentTimeout>(schedule.Message));
        Assert.True(timeoutSchedule.ScheduledEnqueueTime > DateTimeOffset.UtcNow.AddDays(6));
        Assert.Equal(
            "command-message-1",
            invoiceCreatedSend.Message.ApplicationProperties[MiniBusHeaderNames.CausationId]);
        Assert.Equal(
            "billing-correlation-1",
            invoiceCreatedSend.Message.ApplicationProperties[MiniBusHeaderNames.CorrelationId]);
    }

    private static ServiceProvider BuildSampleStyleProvider(RecordingServiceBusSender sender)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBillingMiniBus();
        services.RemoveAll<IAzureServiceBusSender>();
        services.AddSingleton<IAzureServiceBusSender>(sender);

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    internal static ServiceBusReceivedMessage CreateReceivedMessage<TMessage>(
        TMessage message,
        string messageId,
        string correlationId)
    {
        var serializer = new SystemTextJsonMessageSerializer();
        var messageType = typeof(TMessage);
        var properties = new Dictionary<string, object>
        {
            [MiniBusHeaderNames.MessageType] = messageType.AssemblyQualifiedName!,
            [MiniBusHeaderNames.EnclosedMessageTypes] = messageType.AssemblyQualifiedName!,
            [MiniBusHeaderNames.MessageId] = messageId,
            [MiniBusHeaderNames.CorrelationId] = correlationId,
            [MiniBusHeaderNames.ContentType] = "application/json"
        };

        return ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: serializer.Serialize(message!, messageType),
            messageId: messageId,
            correlationId: correlationId,
            contentType: "application/json",
            subject: messageType.AssemblyQualifiedName,
            properties: properties);
    }

    private static ServiceBusReceivedMessage ToReceivedMessage(ServiceBusMessage message)
    {
        return ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: message.Body,
            messageId: message.MessageId,
            correlationId: message.CorrelationId,
            contentType: message.ContentType,
            subject: message.Subject,
            properties: message.ApplicationProperties.ToDictionary(
                pair => pair.Key,
                pair => pair.Value));
    }

    private static bool HasMessageType<TMessage>(ServiceBusMessage message)
    {
        return message.ApplicationProperties.TryGetValue(MiniBusHeaderNames.MessageType, out var value)
               && value is string typeName
               && typeName.Contains(typeof(TMessage).FullName!, StringComparison.Ordinal);
    }

    internal sealed class RecordingServiceBusSender : IAzureServiceBusSender
    {
        public List<(string Destination, ServiceBusMessage Message)> Sends { get; } = new();

        public List<(string Destination, ServiceBusMessage Message, DateTimeOffset ScheduledEnqueueTime)> Schedules { get; } = new();

        public Task SendAsync(
            string destination,
            ServiceBusMessage message,
            CancellationToken cancellationToken = default)
        {
            Sends.Add((destination, message));
            return Task.CompletedTask;
        }

        public Task<long> ScheduleAsync(
            string destination,
            ServiceBusMessage message,
            DateTimeOffset scheduledEnqueueTime,
            CancellationToken cancellationToken = default)
        {
            Schedules.Add((destination, message, scheduledEnqueueTime));
            return Task.FromResult(1L);
        }
    }

    internal sealed class RecordingMessageActions : IMiniBusMessageActions
    {
        public ServiceBusReceivedMessage? CompletedMessage { get; private set; }

        public ServiceBusReceivedMessage? DeadLetteredMessage { get; private set; }

        public Task CompleteMessageAsync(
            ServiceBusReceivedMessage message,
            CancellationToken cancellationToken = default)
        {
            CompletedMessage = message;
            return Task.CompletedTask;
        }

        public Task DeadLetterMessageAsync(
            ServiceBusReceivedMessage message,
            string deadLetterReason,
            string? deadLetterErrorDescription = null,
            CancellationToken cancellationToken = default)
        {
            DeadLetteredMessage = message;
            return Task.CompletedTask;
        }
    }
}

public sealed class SqlBackedReferenceSolutionAcceptanceTests :
    IClassFixture<SqlBackedReferenceSolutionAcceptanceTests.SqlServerFixture>
{
    private const string ConnectionStringEnvironmentVariable = "MINIBUS_SQLSERVER_TEST_CONNECTION_STRING";
    private const string SqlServerImage = "mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04";
    private readonly SqlServerFixture _fixture;

    public SqlBackedReferenceSolutionAcceptanceTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    [SqlServerFact]
    public async Task Tier2_SqlBackedBillingWorkflow_RecordsDurableProcessingEffects()
    {
        await using var database = await _fixture.CreateDatabaseAsync();
        var sender = new ReferenceSolutionAcceptanceTests.RecordingServiceBusSender();
        using var provider = BuildSqlBackedProvider(database, sender);
        var processor = provider.GetRequiredService<MiniBusProcessor>();
        var invoiceId = "invoice-sql-1";
        var customerId = "customer-sql-1";
        var commandActions = new ReferenceSolutionAcceptanceTests.RecordingMessageActions();

        await processor.ProcessAsync(
            ReferenceSolutionAcceptanceTests.CreateReceivedMessage(
                new CreateInvoice(invoiceId, customerId, 123.45m),
                messageId: "command-message-sql-1",
                correlationId: "billing-correlation-sql-1"),
            commandActions);

        Assert.NotNull(commandActions.CompletedMessage);
        Assert.Null(commandActions.DeadLetteredMessage);
        Assert.Empty(sender.Sends);
        Assert.Empty(sender.Schedules);

        var eventActions = new ReferenceSolutionAcceptanceTests.RecordingMessageActions();
        await processor.ProcessAsync(
            ReferenceSolutionAcceptanceTests.CreateReceivedMessage(
                new InvoiceCreated(invoiceId, customerId, 123.45m),
                messageId: "event-message-sql-1",
                correlationId: "billing-correlation-sql-1"),
            eventActions);

        Assert.NotNull(eventActions.CompletedMessage);
        Assert.Null(eventActions.DeadLetteredMessage);
        Assert.Equal(2, await database.CountRowsAsync(database.InboxTableName));
        Assert.Equal(3, await database.CountRowsAsync(database.OutboxTableName));
        Assert.Equal(1, await database.CountRowsAsync(database.SagaTableName));

        var operationKinds = await database.QueryStringsAsync($"""
            SELECT OperationKind
            FROM {database.OutboxTableName}
            ORDER BY OperationKind;
            """);
        Assert.Contains("Publish", operationKinds);
        Assert.Contains("Schedule", operationKinds);
        Assert.Contains("Send", operationKinds);

        var scheduledMessageType = await database.QueryScalarAsync<string>($"""
            SELECT MessageType
            FROM {database.OutboxTableName}
            WHERE OperationKind = 'Schedule';
            """);
        Assert.Contains(typeof(InvoicePaymentTimeout).FullName!, scheduledMessageType, StringComparison.Ordinal);

        var sagaCorrelationId = await database.QueryScalarAsync<string>($"""
            SELECT CorrelationId
            FROM {database.SagaTableName};
            """);
        Assert.Equal(invoiceId, sagaCorrelationId);
    }

    private static ServiceProvider BuildSqlBackedProvider(
        SqlServerTestDatabase database,
        ReferenceSolutionAcceptanceTests.RecordingServiceBusSender sender)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBillingMiniBus();
        services.RemoveAll<IAzureServiceBusSender>();
        services.RemoveAll<ISagaPersistence>();
        services.AddSingleton<IAzureServiceBusSender>(sender);
        services.AddSingleton<IMiniBusOutboxDispatcher>(
            serviceProvider => serviceProvider.GetRequiredService<AzureServiceBusTransportDispatcher>());
        services.AddMiniBusSqlPersistence(options =>
        {
            options.SchemaName = database.SchemaName;
            options.ConnectionFactory = database.CreateConnection;
        });

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    public sealed class SqlServerFixture : IAsyncLifetime
    {
        private readonly string? _externalConnectionString;
        private Testcontainers.MsSql.MsSqlContainer? _container;
        private Exception? _startupException;

        public SqlServerFixture()
        {
            _externalConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        }

        public async Task InitializeAsync()
        {
            if (!string.IsNullOrWhiteSpace(_externalConnectionString))
            {
                return;
            }

            try
            {
                _container = new Testcontainers.MsSql.MsSqlBuilder()
                    .WithImage(SqlServerImage)
                    .WithCreateParameterModifier(parameters =>
                    {
                        parameters.Platform = "linux/amd64";
                    })
                    .Build();

                await _container.StartAsync();
            }
            catch (Exception exception)
            {
                _startupException = exception;
            }
        }

        public async Task DisposeAsync()
        {
            if (_container is not null)
            {
                await _container.DisposeAsync();
            }
        }

        internal async Task<SqlServerTestDatabase> CreateDatabaseAsync()
        {
            if (_startupException is not null)
            {
                throw new InvalidOperationException(
                    $"SQL Server Testcontainers startup failed for {SqlServerImage} on linux/amd64. " +
                    "On Apple Silicon, ensure Docker Desktop can run amd64 Linux containers. " +
                    $"Alternatively set {ConnectionStringEnvironmentVariable}. " +
                    $"Original error: {_startupException.Message}");
            }

            var connectionString = !string.IsNullOrWhiteSpace(_externalConnectionString)
                ? _externalConnectionString
                : _container?.GetConnectionString();

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Set {ConnectionStringEnvironmentVariable} or enable Docker to run SQL Server-backed MiniBus acceptance tests.");
            }

            var database = new SqlServerTestDatabase(
                connectionString,
                $"MiniBusAcceptance_{Guid.NewGuid():N}");
            await database.ApplySchemaAsync();
            return database;
        }
    }

    internal sealed class SqlServerTestDatabase : IAsyncDisposable
    {
        public SqlServerTestDatabase(string connectionString, string schemaName)
        {
            ConnectionString = connectionString;
            SchemaName = schemaName;
            InboxTableName = $"[{SchemaName}].[Inbox]";
            OutboxTableName = $"[{SchemaName}].[Outbox]";
            SagaTableName = $"[{SchemaName}].[Sagas]";
        }

        public string ConnectionString { get; }

        public string SchemaName { get; }

        public string InboxTableName { get; }

        public string OutboxTableName { get; }

        public string SagaTableName { get; }

        public SqlConnection CreateConnection()
        {
            return new SqlConnection(ConnectionString);
        }

        public async Task<int> CountRowsAsync(string tableName)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {tableName};";
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        public async Task<IReadOnlyList<string>> QueryStringsAsync(string commandText)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;

            var values = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                values.Add(reader.GetString(0));
            }

            return values;
        }

        public async Task<T?> QueryScalarAsync<T>(string commandText)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;

            var value = await command.ExecuteScalarAsync();
            if (value is null or DBNull)
            {
                return default;
            }

            return (T)value;
        }

        public async Task ApplySchemaAsync()
        {
            var schemaDirectory = Path.GetFullPath(
                "../../../../../src/MiniBus.Persistence.Sql/Schema",
                AppContext.BaseDirectory);
            var scriptPaths = Directory.GetFiles(schemaDirectory, "*.sql");
            var invalidScript = scriptPaths
                .Select(Path.GetFileName)
                .FirstOrDefault(name => !IsVersionedSchemaScriptName(name));

            if (invalidScript is not null)
            {
                throw new InvalidOperationException(
                    $"MiniBus SQL schema scripts must use a three-digit migration prefix such as '001-'. Invalid script: {invalidScript}");
            }

            foreach (var scriptPath in scriptPaths.Order(StringComparer.Ordinal))
            {
                var script = File.ReadAllText(scriptPath);
                script = script
                    .Replace("SCHEMA_ID(N'MiniBus')", $"SCHEMA_ID(N'{SchemaName}')", StringComparison.Ordinal)
                    .Replace("CREATE SCHEMA MiniBus", $"CREATE SCHEMA [{SchemaName}]", StringComparison.Ordinal)
                    .Replace("OBJECT_ID(N'MiniBus.Outbox'", $"OBJECT_ID(N'{SchemaName}.Outbox'", StringComparison.Ordinal)
                    .Replace("OBJECT_ID(N'MiniBus.Inbox'", $"OBJECT_ID(N'{SchemaName}.Inbox'", StringComparison.Ordinal)
                    .Replace("N'MiniBus.Outbox'", $"N'{SchemaName}.Outbox'", StringComparison.Ordinal)
                    .Replace("N'MiniBus'", $"N'{SchemaName}'", StringComparison.Ordinal)
                    .Replace("MiniBus.", $"{SchemaName}.", StringComparison.Ordinal);

                await ExecuteNonQueryAsync(script);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await ExecuteNonQueryAsync($"""
                IF OBJECT_ID(N'{SchemaName}.Outbox', N'U') IS NOT NULL
                    DROP TABLE {OutboxTableName};

                IF OBJECT_ID(N'{SchemaName}.Sagas', N'U') IS NOT NULL
                    DROP TABLE {SagaTableName};

                IF OBJECT_ID(N'{SchemaName}.Inbox', N'U') IS NOT NULL
                    DROP TABLE {InboxTableName};

                IF SCHEMA_ID(N'{SchemaName}') IS NOT NULL
                    EXEC(N'DROP SCHEMA [{SchemaName}]');
                """);
        }

        private async Task ExecuteNonQueryAsync(string commandText)
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            await command.ExecuteNonQueryAsync();
        }

        private static bool IsVersionedSchemaScriptName(string? fileName)
        {
            return fileName is { Length: > 4 }
                   && char.IsDigit(fileName[0])
                   && char.IsDigit(fileName[1])
                   && char.IsDigit(fileName[2])
                   && fileName[3] == '-';
        }
    }

    private sealed class SqlServerFactAttribute : FactAttribute
    {
        public SqlServerFactAttribute()
        {
            Timeout = 180_000;

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable))
                && !DockerSocketIsReachable())
            {
                Skip = "Docker is not reachable, and MINIBUS_SQLSERVER_TEST_CONNECTION_STRING is not set. " +
                       "Start Docker Desktop with linux/amd64 container support or configure an external SQL Server/Azure SQL test connection string.";
            }
        }

        private static bool DockerSocketIsReachable()
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST")))
            {
                return true;
            }

            return UnixSocketIsReachable("/var/run/docker.sock")
                   || UnixSocketIsReachable(Path.Combine(
                       Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                       ".docker",
                       "run",
                       "docker.sock"));
        }

        private static bool UnixSocketIsReachable(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                using var socket = new System.Net.Sockets.Socket(
                    System.Net.Sockets.AddressFamily.Unix,
                    System.Net.Sockets.SocketType.Stream,
                    System.Net.Sockets.ProtocolType.Unspecified);
                var connectTask = socket.ConnectAsync(new System.Net.Sockets.UnixDomainSocketEndPoint(path));
                return connectTask.Wait(TimeSpan.FromMilliseconds(250)) && socket.Connected;
            }
            catch
            {
                return false;
            }
        }
    }
}
