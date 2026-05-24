using Azure.Messaging.ServiceBus;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Serialization;
using MiniBus.Samples.Contracts.Billing;
using CoreHeaderNames = MiniBus.Core.Headers.MiniBusHeaderNames;

namespace MiniBus.Samples.Billing.FunctionApp;

public static class BillingSampleSeeder
{
    public static bool IsSeedCommand(IReadOnlyList<string> args)
    {
        return args.Count > 0
               && (string.Equals(args[0], "seed", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(args[0], "--seed", StringComparison.OrdinalIgnoreCase));
    }

    public static async Task<BillingSeedResult> SendCreateInvoiceAsync(
        string connectionString,
        CreateInvoice? command = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        command ??= new CreateInvoice(
            InvoiceId: $"invoice-{Guid.NewGuid():N}",
            CustomerId: "sample-customer",
            Amount: 123.45m);
        correlationId ??= $"billing-{Guid.NewGuid():N}";

        var messageFactory = new AzureServiceBusMessageFactory(new SystemTextJsonMessageSerializer());
        var serviceBusMessage = await messageFactory.CreateMessageAsync(
            command,
            typeof(CreateInvoice),
            new Dictionary<string, string>
            {
                [CoreHeaderNames.CorrelationId] = correlationId
            }, cancellationToken);
        serviceBusMessage.CorrelationId = correlationId;

        await using var client = new ServiceBusClient(
            connectionString,
            new ServiceBusClientOptions
            {
                RetryOptions =
                {
                    MaxRetries = 0,
                    TryTimeout = TimeSpan.FromSeconds(15)
                }
            });
        await using var sender = client.CreateSender(BillingTopology.InputQueue);
        try
        {
            await sender.SendMessageAsync(serviceBusMessage, cancellationToken).ConfigureAwait(false);
        }
        catch (ServiceBusException exception) when (exception.Reason == ServiceBusFailureReason.ServiceTimeout)
        {
            throw new InvalidOperationException(
                "The Billing seed command timed out sending to Service Bus. " +
                "For the local emulator, wait until its topology is ready. " +
                "If its SQL container was recreated, recreate or restart the emulator container too.",
                exception);
        }

        return new BillingSeedResult(
            command.InvoiceId,
            command.CustomerId,
            serviceBusMessage.MessageId,
            correlationId);
    }
}

public sealed record BillingSeedResult(
    string InvoiceId,
    string CustomerId,
    string MessageId,
    string CorrelationId);
