using System.Diagnostics;
using System.Net.Sockets;
using Azure.Messaging.ServiceBus;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.AzureFunctions.Processing;
using MiniBus.Core.Headers;
using MiniBus.Core.Serialization;
using MiniBus.Samples.Contracts.Billing;
using MiniBus.Samples.Contracts.Inventory;
using MiniBus.Samples.FunctionApp;
using MiniBus.Samples.Inventory.FunctionApp;
using MiniBus.Samples.Inventory.FunctionApp.Handlers;
using Xunit.Sdk;

namespace MiniBus.AcceptanceTests;

[CollectionDefinition(ServiceBusEmulatorCollection.Name, DisableParallelization = true)]
public sealed class ServiceBusEmulatorCollection
{
    public const string Name = "Service Bus emulator";
}

[Collection(ServiceBusEmulatorCollection.Name)]
public sealed class ServiceBusEmulatorBillingWorkflowTests
{
    private const string ConnectionStringEnvironmentVariable = "MINIBUS_SERVICEBUS_EMULATOR_CONNECTION_STRING";
    private const string ReceiveTimeoutSecondsEnvironmentVariable = "MINIBUS_SERVICEBUS_EMULATOR_RECEIVE_TIMEOUT_SECONDS";
    private static readonly string[] SampleFunctionHostProcessMarkers =
    [
        "samples/MiniBus.Samples.FunctionApp/bin/",
        "samples\\MiniBus.Samples.FunctionApp\\bin\\",
        "samples/MiniBus.Samples.Inventory.FunctionApp/bin/",
        "samples\\MiniBus.Samples.Inventory.FunctionApp\\bin\\",
        "MiniBus.Samples.FunctionApp.dll",
        "MiniBus.Samples.Inventory.FunctionApp.dll"
    ];

    [ServiceBusEmulatorFact]
    public async Task BillingWorkflow_DispatchesThroughServiceBusEmulator()
    {
        await SkipWhenEmulatorIsUnavailableAsync();

        var connectionString = GetConnectionString();
        await SkipWhenInventoryQueueIsUnavailableAsync(connectionString);
        await SkipWhenSampleFunctionHostsAreRunningAsync();

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
        var commandMessage = await ReceiveBillingCommandRequiredAsync(
            client,
            commandReceiver,
            "Billing command",
            seed.CorrelationId);
        await processor.ProcessAsync(commandMessage);
        await commandReceiver.CompleteMessageAsync(commandMessage);

        await using var inventoryProvider = BuildInventoryProvider(connectionString);
        var inventoryProcessor = inventoryProvider.GetRequiredService<MiniBusProcessor>();
        await using var inventoryReceiver = client.CreateReceiver(BillingTopology.InventoryQueue);
        var inventoryMessage = await ReceiveRequiredAsync(
            inventoryReceiver,
            "ReserveInventory command",
            seed.CorrelationId);
        var reservationCommand = Deserialize<ReserveInventory>(inventoryMessage);

        Assert.Equal(command.InvoiceId, reservationCommand.InvoiceId);
        Assert.Equal(command.CustomerId, reservationCommand.CustomerId);
        Assert.Equal("sample-sku", reservationCommand.Sku);
        Assert.Equal(1, reservationCommand.Quantity);

        await inventoryProcessor.ProcessAsync(inventoryMessage);
        await inventoryReceiver.CompleteMessageAsync(inventoryMessage);

        var reservation = Assert.Single(
            inventoryProvider.GetRequiredService<InventoryReservationLog>().Reservations);
        Assert.Equal(command.InvoiceId, reservation.InvoiceId);
        Assert.Equal(command.CustomerId, reservation.CustomerId);

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
        var connectionString = GetConnectionString();
        await SkipWhenInventoryQueueIsUnavailableAsync(connectionString);
        await SkipWhenSampleFunctionHostsAreRunningAsync();

        await BillingSampleSqlSchemaApplier.ApplyAsync(
            BillingSampleSqlPersistence.LocalConnectionString);

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
        var commandMessage = await ReceiveBillingCommandRequiredAsync(
            client,
            commandReceiver,
            "SQL-backed Billing command",
            seed.CorrelationId);
        await processor.ProcessAsync(commandMessage);
        await commandReceiver.CompleteMessageAsync(commandMessage);

        Assert.Equal(3, await CountPendingOutboxRowsAsync(commandMessage.MessageId));

        var commandDrainDispatched = await dispatcher.DispatchPendingAsync();

        Assert.True(commandDrainDispatched >= 3);
        Assert.Equal(0, await CountPendingOutboxRowsAsync(commandMessage.MessageId));
        Assert.Equal(3, await CountDispatchedOutboxRowsAsync(commandMessage.MessageId));

        await using var inventoryReceiver = client.CreateReceiver(BillingTopology.InventoryQueue);
        var inventoryMessage = await ReceiveRequiredAsync(
            inventoryReceiver,
            "SQL-backed ReserveInventory command",
            seed.CorrelationId);
        var reservationCommand = Deserialize<ReserveInventory>(inventoryMessage);
        await inventoryReceiver.CompleteMessageAsync(inventoryMessage);

        Assert.Equal(command.InvoiceId, reservationCommand.InvoiceId);
        Assert.Equal(command.CustomerId, reservationCommand.CustomerId);

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

    private static ServiceProvider BuildInventoryProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInventoryMiniBus(ReferenceSolutionAcceptanceTests.CreateInventoryConfiguration(connectionString));

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
        var timeout = GetReceiveTimeout();
        return await ReceiveOptionalAsync(receiver, correlationId, timeout).ConfigureAwait(false)
               ?? throw CreateMissingMessageException(description, correlationId, timeout);
    }

    private static async Task<ServiceBusReceivedMessage> ReceiveBillingCommandRequiredAsync(
        ServiceBusClient client,
        ServiceBusReceiver receiver,
        string description,
        string correlationId)
    {
        var timeout = GetReceiveTimeout();
        var message = await ReceiveOptionalAsync(receiver, correlationId, timeout).ConfigureAwait(false);
        if (message is not null)
        {
            return message;
        }

        var downstreamEvidence = await FindDownstreamEvidenceAsync(client, correlationId).ConfigureAwait(false);
        if (downstreamEvidence.Length > 0)
        {
            throw new InvalidOperationException(
                $"The Azure Service Bus emulator did not deliver the expected {description} " +
                $"with correlation id '{correlationId}' because it appears to have already been consumed. " +
                $"Found downstream message evidence in {string.Join(", ", downstreamEvidence)} after waiting {timeout.TotalSeconds:0.#} seconds. " +
                "Stop any local Billing or Inventory Function Apps before running this acceptance test so the test owns " +
                "`billing-queue`, `inventory-queue`, and the `billing` subscription.");
        }

        throw CreateMissingMessageException(description, correlationId, timeout);
    }

    private static async Task<ServiceBusReceivedMessage?> ReceiveOptionalAsync(
        ServiceBusReceiver receiver,
        string correlationId,
        TimeSpan timeout)
    {
        var unmatchedMessages = new List<ServiceBusReceivedMessage>();
        var deadline = DateTimeOffset.UtcNow.Add(timeout);

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

        return null;
    }

    private static async Task<string[]> FindDownstreamEvidenceAsync(
        ServiceBusClient client,
        string correlationId)
    {
        var evidence = new List<string>();

        if (await PeekHasCorrelationIdAsync(
                client.CreateReceiver(BillingTopology.InventoryQueue),
                correlationId)
            .ConfigureAwait(false))
        {
            evidence.Add(BillingTopology.InventoryQueue);
        }

        if (await PeekHasCorrelationIdAsync(
                client.CreateReceiver(BillingTopology.ReceiptsQueue),
                correlationId)
            .ConfigureAwait(false))
        {
            evidence.Add(BillingTopology.ReceiptsQueue);
        }

        if (await PeekHasCorrelationIdAsync(
                client.CreateReceiver(BillingTopology.EventsTopic, BillingTopology.BillingSubscription),
                correlationId)
            .ConfigureAwait(false))
        {
            evidence.Add($"{BillingTopology.EventsTopic}/{BillingTopology.BillingSubscription}");
        }

        return evidence.ToArray();
    }

    private static async Task<bool> PeekHasCorrelationIdAsync(
        ServiceBusReceiver receiver,
        string correlationId)
    {
        await using (receiver.ConfigureAwait(false))
        {
            long? fromSequenceNumber = null;
            for (var page = 0; page < 5; page++)
            {
                var messages = await receiver
                    .PeekMessagesAsync(100, fromSequenceNumber)
                    .ConfigureAwait(false);
                if (messages.Count == 0)
                {
                    return false;
                }

                if (messages.Any(message => HasCorrelationId(message, correlationId)))
                {
                    return true;
                }

                fromSequenceNumber = messages[^1].SequenceNumber + 1;
            }

            return false;
        }
    }

    private static InvalidOperationException CreateMissingMessageException(
        string description,
        string correlationId,
        TimeSpan timeout)
    {
        return new InvalidOperationException(
            $"The Azure Service Bus emulator did not deliver the expected {description} " +
            $"with correlation id '{correlationId}' after waiting {timeout.TotalSeconds:0.#} seconds. " +
            $"Set {ReceiveTimeoutSecondsEnvironmentVariable} to a larger value on slower emulator hosts. " +
            "Make sure the emulator has loaded the sample topology and stop any local Function Apps before running " +
            "this acceptance test so another consumer cannot take the message.");
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

    private static TimeSpan GetReceiveTimeout()
    {
        var configuredTimeout = Environment.GetEnvironmentVariable(ReceiveTimeoutSecondsEnvironmentVariable);
        if (int.TryParse(configuredTimeout, out var seconds) && seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return TimeSpan.FromSeconds(10);
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

    private static async Task SkipWhenInventoryQueueIsUnavailableAsync(string connectionString)
    {
        await using var client = new ServiceBusClient(
            connectionString,
            new ServiceBusClientOptions
            {
                RetryOptions =
                {
                    MaxRetries = 0,
                    TryTimeout = TimeSpan.FromSeconds(3)
                }
            });
        await using var receiver = client.CreateReceiver(BillingTopology.InventoryQueue);

        try
        {
            _ = await receiver.PeekMessageAsync();
        }
        catch (ServiceBusException exception) when (exception.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
        {
            throw SkipException.ForSkip(
                $"The Azure Service Bus emulator topology does not include `{BillingTopology.InventoryQueue}`. " +
                "Restart the sample emulator compose stack so it loads the current Config.json before running this scenario.");
        }
    }

    private static async Task SkipWhenSampleFunctionHostsAreRunningAsync()
    {
        var runningHosts = await GetRunningSampleFunctionHostsAsync().ConfigureAwait(false);
        if (runningHosts.Length == 0)
        {
            return;
        }

        throw SkipException.ForSkip(
            "A local Billing or Inventory Function App is running and can consume messages from the emulator queues. " +
            "Stop the local Function Apps before running ServiceBusEmulatorBillingWorkflowTests. " +
            $"Detected: {string.Join("; ", runningHosts)}");
    }

    private static async Task<string[]> GetRunningSampleFunctionHostsAsync()
    {
        try
        {
            var output = OperatingSystem.IsWindows()
                ? await GetWindowsProcessListAsync().ConfigureAwait(false)
                : await ReadProcessListAsync("ps", "-axo pid=,command=").ConfigureAwait(false);

            var currentProcessId = Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => !line.StartsWith(currentProcessId, StringComparison.Ordinal))
                .Where(IsSampleFunctionHostProcess)
                .Select(TrimProcessCommand)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or OperationCanceledException)
        {
            return [];
        }
    }

    private static async Task<string> GetWindowsProcessListAsync()
    {
        const string command =
            "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " +
            "\"Get-CimInstance Win32_Process | ForEach-Object { '{0} {1}' -f $_.ProcessId, $_.CommandLine }\"";

        try
        {
            return await ReadProcessListAsync("powershell.exe", command).ConfigureAwait(false);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return await ReadProcessListAsync("pwsh", command).ConfigureAwait(false);
        }
    }

    private static async Task<string> ReadProcessListAsync(string fileName, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(2));
        if (await Task.WhenAny(outputTask, timeoutTask).ConfigureAwait(false) != outputTask)
        {
            KillProcessTree(process);
            await process.WaitForExitAsync().ConfigureAwait(false);
            return string.Empty;
        }

        var output = await outputTask.ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        return process.ExitCode == 0 ? output : string.Empty;
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }

    private static bool IsSampleFunctionHostProcess(string processLine)
    {
        return SampleFunctionHostProcessMarkers.Any(marker =>
            processLine.Contains(marker, StringComparison.Ordinal));
    }

    private static string TrimProcessCommand(string processLine)
    {
        const int maxLength = 160;
        var command = processLine.Trim();
        return command.Length <= maxLength
            ? command
            : string.Concat(command.AsSpan(0, maxLength), "...");
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
