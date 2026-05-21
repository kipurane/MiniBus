using System.Net.Sockets;
using Azure.Messaging.ServiceBus;
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

    private sealed class ServiceBusEmulatorFactAttribute : FactAttribute
    {
        public ServiceBusEmulatorFactAttribute()
        {
            Timeout = 120_000;
        }
    }
}
