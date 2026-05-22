using System.Net.Sockets;
using Azure.Messaging.ServiceBus;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.Processing;
using MiniBus.Core.Headers;
using MiniBus.Core.Serialization;
using MiniBus.Samples.FunctionApp;
using MiniBus.Samples.FunctionApp.Contracts;
using Xunit.Sdk;

namespace MiniBus.AcceptanceTests;

public sealed class ServiceBusEmulatorBillingWorkflowTests
{
    private const string ConnectionStringEnvironmentVariable = "MINIBUS_SERVICEBUS_EMULATOR_CONNECTION_STRING";

    [ServiceBusEmulatorFact]
    public async Task BillingWorkflow_DispatchesThroughServiceBusEmulator()
    {
        await SkipWhenEmulatorIsUnavailableAsync();

        var connectionString = GetConnectionString();
        await using var provider = BuildProvider(connectionString);
        var processor = provider.GetRequiredService<MiniBusProcessor>();
        await using var client = new ServiceBusClient(connectionString);
        var command = new CreateInvoice(
            InvoiceId: $"emulator-invoice-{Guid.NewGuid():N}",
            CustomerId: "emulator-customer",
            Amount: 123.45m);

        var seed = await BillingSampleSeeder.SendCreateInvoiceAsync(
            connectionString,
            command,
            correlationId: $"emulator-billing-{Guid.NewGuid():N}");

        await using var commandReceiver = client.CreateReceiver(BillingTopology.InputQueue);
        var commandMessage = await ReceiveRequiredAsync(
            commandReceiver,
            "Billing command",
            seed.CorrelationId);
        await processor.ProcessAsync(commandMessage);
        await commandReceiver.CompleteMessageAsync(commandMessage);

        await using var receiptReceiver = client.CreateReceiver(BillingTopology.ReceiptsQueue);
        var receiptMessage = await ReceiveRequiredAsync(
            receiptReceiver,
            "receipt command",
            seed.CorrelationId);
        var receipt = Deserialize<SendInvoiceReceipt>(receiptMessage);
        await receiptReceiver.CompleteMessageAsync(receiptMessage);

        Assert.Equal(command.InvoiceId, receipt.InvoiceId);
        Assert.Equal(command.CustomerId, receipt.CustomerId);

        await using var eventReceiver = client.CreateReceiver(
            BillingTopology.EventsTopic,
            BillingTopology.BillingSubscription);
        var eventMessage = await ReceiveRequiredAsync(
            eventReceiver,
            "InvoiceCreated event",
            seed.CorrelationId);
        var invoiceCreated = Deserialize<InvoiceCreated>(eventMessage);

        Assert.Equal(command.InvoiceId, invoiceCreated.InvoiceId);
        Assert.Equal(command.CustomerId, invoiceCreated.CustomerId);

        await processor.ProcessAsync(eventMessage);
        await eventReceiver.CompleteMessageAsync(eventMessage);
    }

    [SqlBackedServiceBusEmulatorFact]
    public async Task SqlBackedBillingWorkflow_DrainsOutboxThroughServiceBusEmulator()
    {
        await SkipWhenEmulatorIsUnavailableAsync();
        await SkipWhenSqlServerIsUnavailableAsync();
        await BillingSampleSqlSchemaApplier.ApplyAsync(
            BillingSampleSqlPersistence.LocalConnectionString);

        var connectionString = GetConnectionString();
        await using var provider = BuildSqlBackedProvider(connectionString);
        var processor = provider.GetRequiredService<MiniBusProcessor>();
        var dispatcher = provider.GetRequiredService<MiniBus.Persistence.Sql.SqlMiniBusOutboxDispatcher>();
        await using var client = new ServiceBusClient(connectionString);
        var command = new CreateInvoice(
            InvoiceId: $"emulator-sql-invoice-{Guid.NewGuid():N}",
            CustomerId: "emulator-sql-customer",
            Amount: 123.45m);

        var seed = await BillingSampleSeeder.SendCreateInvoiceAsync(
            connectionString,
            command,
            correlationId: $"emulator-sql-billing-{Guid.NewGuid():N}");

        await using var commandReceiver = client.CreateReceiver(BillingTopology.InputQueue);
        var commandMessage = await ReceiveRequiredAsync(
            commandReceiver,
            "SQL-backed Billing command",
            seed.CorrelationId);
        await processor.ProcessAsync(commandMessage);
        await commandReceiver.CompleteMessageAsync(commandMessage);

        Assert.Equal(2, await CountPendingOutboxRowsAsync(commandMessage.MessageId));

        var commandDrainDispatched = await dispatcher.DispatchPendingAsync();

        Assert.True(commandDrainDispatched >= 2);
        Assert.Equal(0, await CountPendingOutboxRowsAsync(commandMessage.MessageId));
        Assert.Equal(2, await CountDispatchedOutboxRowsAsync(commandMessage.MessageId));

        await using var receiptReceiver = client.CreateReceiver(BillingTopology.ReceiptsQueue);
        var receiptMessage = await ReceiveRequiredAsync(
            receiptReceiver,
            "SQL-backed receipt command",
            seed.CorrelationId);
        var receipt = Deserialize<SendInvoiceReceipt>(receiptMessage);
        await receiptReceiver.CompleteMessageAsync(receiptMessage);

        Assert.Equal(command.InvoiceId, receipt.InvoiceId);
        Assert.Equal(command.CustomerId, receipt.CustomerId);

        await using var eventReceiver = client.CreateReceiver(
            BillingTopology.EventsTopic,
            BillingTopology.BillingSubscription);
        var eventMessage = await ReceiveRequiredAsync(
            eventReceiver,
            "SQL-backed InvoiceCreated event",
            seed.CorrelationId);
        var invoiceCreated = Deserialize<InvoiceCreated>(eventMessage);

        Assert.Equal(command.InvoiceId, invoiceCreated.InvoiceId);
        Assert.Equal(command.CustomerId, invoiceCreated.CustomerId);

        await processor.ProcessAsync(eventMessage);
        await eventReceiver.CompleteMessageAsync(eventMessage);

        Assert.Equal(1, await CountPendingOutboxRowsAsync(eventMessage.MessageId));

        var timeoutDrainDispatched = await dispatcher.DispatchPendingAsync();

        Assert.True(timeoutDrainDispatched >= 1);
        Assert.Equal(0, await CountPendingOutboxRowsAsync(eventMessage.MessageId));
        Assert.Equal(1, await CountDispatchedOutboxRowsAsync(eventMessage.MessageId));
    }

    private static ServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBillingMiniBus(ReferenceSolutionAcceptanceTests.CreateBillingConfiguration(connectionString));

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static ServiceProvider BuildSqlBackedProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddBillingMiniBus(ReferenceSolutionAcceptanceTests.CreateBillingConfiguration(
            connectionString,
            BillingSampleSqlPersistence.LocalConnectionString));

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static TMessage Deserialize<TMessage>(ServiceBusReceivedMessage message)
    {
        return (TMessage)new SystemTextJsonMessageSerializer()
            .Deserialize(message.Body, typeof(TMessage));
    }

    private static async Task<ServiceBusReceivedMessage> ReceiveRequiredAsync(
        ServiceBusReceiver receiver,
        string description,
        string correlationId)
    {
        var unmatchedMessages = new List<ServiceBusReceivedMessage>();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(15);

        try
        {
            while (DateTimeOffset.UtcNow < deadline)
            {
                var receiveWindow = deadline - DateTimeOffset.UtcNow;
                if (receiveWindow <= TimeSpan.Zero)
                {
                    break;
                }

                var message = await receiver.ReceiveMessageAsync(receiveWindow);

                if (message is null)
                {
                    continue;
                }

                if (HasCorrelationId(message, correlationId))
                {
                    return message;
                }

                unmatchedMessages.Add(message);
            }
        }
        finally
        {
            foreach (var unmatchedMessage in unmatchedMessages)
            {
                try
                {
                    await receiver.AbandonMessageAsync(unmatchedMessage);
                }
                catch (ServiceBusException)
                {
                    // Preserve the receive failure when an unmatched message lock has already expired.
                }
            }
        }

        throw new InvalidOperationException(
            $"The Azure Service Bus emulator did not deliver the expected {description} " +
            $"with correlation id '{correlationId}'.");
    }

    private static bool HasCorrelationId(ServiceBusReceivedMessage message, string correlationId)
    {
        return string.Equals(message.CorrelationId, correlationId, StringComparison.Ordinal)
               || (message.ApplicationProperties.TryGetValue(MiniBusHeaderNames.CorrelationId, out var headerValue)
                   && headerValue is string headerCorrelationId
                   && string.Equals(headerCorrelationId, correlationId, StringComparison.Ordinal));
    }

    private static string GetConnectionString()
    {
        return Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable)
               ?? BillingTopology.EmulatorConnectionString;
    }

    private static async Task SkipWhenEmulatorIsUnavailableAsync()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable))
            || await TcpPortIsReachableAsync("localhost", 5672))
        {
            return;
        }

        throw SkipException.ForSkip(
            "The Azure Service Bus emulator is not reachable on localhost:5672. " +
            $"Start the Billing sample emulator compose file or set {ConnectionStringEnvironmentVariable}.");
    }

    private static async Task SkipWhenSqlServerIsUnavailableAsync()
    {
        if (await TcpPortIsReachableAsync("localhost", 14333))
        {
            return;
        }

        throw SkipException.ForSkip(
            "The Billing sample SQL Server endpoint is not reachable on localhost:14333. " +
            "Start the current Billing sample emulator compose file before running this scenario.");
    }

    private static async Task<bool> TcpPortIsReachableAsync(string host, int port)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(3);

        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using var client = new TcpClient();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await client.ConnectAsync(host, port, timeout.Token);
                return client.Connected;
            }
            catch (Exception exception) when (exception is SocketException or OperationCanceledException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200));
            }
        }

        return false;
    }

    private static Task<int> CountPendingOutboxRowsAsync(string incomingMessageId)
    {
        return CountOutboxRowsAsync(incomingMessageId, dispatched: false);
    }

    private static Task<int> CountDispatchedOutboxRowsAsync(string incomingMessageId)
    {
        return CountOutboxRowsAsync(incomingMessageId, dispatched: true);
    }

    private static async Task<int> CountOutboxRowsAsync(string incomingMessageId, bool dispatched)
    {
        await using var connection = new SqlConnection(BillingSampleSqlPersistence.LocalConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT COUNT(*)
            FROM [{BillingSampleSqlPersistence.DefaultSchemaName}].[Outbox]
            WHERE IncomingMessageId = @IncomingMessageId
              AND DispatchedUtc IS {(dispatched ? "NOT " : string.Empty)}NULL;
            """;
        command.Parameters.AddWithValue("@IncomingMessageId", incomingMessageId);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private sealed class ServiceBusEmulatorFactAttribute : FactAttribute
    {
        public ServiceBusEmulatorFactAttribute()
        {
            Timeout = 120_000;
        }
    }

    private sealed class SqlBackedServiceBusEmulatorFactAttribute : FactAttribute
    {
        public SqlBackedServiceBusEmulatorFactAttribute()
        {
            Timeout = 120_000;
        }
    }
}
