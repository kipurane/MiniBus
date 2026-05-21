using Azure.Messaging.ServiceBus;
using MiniBus.AzureServiceBus.TransportMessageMapping;
using MiniBus.Core.Serialization;
using MiniBus.Samples.FunctionApp.Contracts;
using CoreHeaderNames = MiniBus.Core.Headers.MiniBusHeaderNames;

namespace MiniBus.Samples.FunctionApp;

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
        var serviceBusMessage = messageFactory.CreateMessage(
            command,
            typeof(CreateInvoice),
            new Dictionary<string, string>
            {
                [CoreHeaderNames.CorrelationId] = correlationId
            });
        serviceBusMessage.CorrelationId = correlationId;

        await using var client = new ServiceBusClient(connectionString);
        await using var sender = client.CreateSender(BillingTopology.InputQueue);
        await sender.SendMessageAsync(serviceBusMessage, cancellationToken).ConfigureAwait(false);

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
