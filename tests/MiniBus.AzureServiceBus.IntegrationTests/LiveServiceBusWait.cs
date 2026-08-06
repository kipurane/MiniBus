using System.Diagnostics;
using Azure.Messaging.ServiceBus;

namespace MiniBus.AzureServiceBus.IntegrationTests;

internal static class LiveServiceBusWait
{
    public static async Task<ServiceBusReceivedMessage> ReceiveAsync(
        ServiceBusReceiver receiver,
        string entityAlias,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receiver);

        var stopwatch = Stopwatch.StartNew();
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            while (stopwatch.Elapsed < timeout)
            {
                var remaining = timeout - stopwatch.Elapsed;
                var wait = remaining < TimeSpan.FromSeconds(3) ? remaining : TimeSpan.FromSeconds(3);
                var message = await receiver
                    .ReceiveMessageAsync(wait, timeoutSource.Token)
                    .ConfigureAwait(false);
                if (message is not null)
                {
                    return message;
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Convert the internal deadline into the diagnostic timeout below.
        }

        throw new TimeoutException(
            $"Timed out after {timeout} waiting for a message from '{entityAlias}'. Last observation: no available message.");
    }

    public static async Task<T> UntilAsync<T>(
        Func<CancellationToken, Task<(bool IsSatisfied, T Value, string Observation)>> observe,
        string expectedCondition,
        string entityAlias,
        TimeSpan timeout,
        TimeSpan? pollingInterval = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(observe);

        var interval = pollingInterval ?? TimeSpan.FromMilliseconds(25);
        var stopwatch = Stopwatch.StartNew();
        var lastObservation = "none";
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            while (stopwatch.Elapsed < timeout)
            {
                var result = await observe(timeoutSource.Token).ConfigureAwait(false);
                lastObservation = result.Observation;
                if (result.IsSatisfied)
                {
                    return result.Value;
                }

                await Task.Delay(interval, timeoutSource.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Convert the internal deadline into the diagnostic timeout below.
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new TimeoutException(
            $"Timed out after {timeout} waiting for {expectedCondition} on '{entityAlias}'. Last observation: {lastObservation}.");
    }
}
