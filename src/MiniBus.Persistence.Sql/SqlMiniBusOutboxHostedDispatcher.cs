using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MiniBus.Persistence.Sql;

internal sealed partial class SqlMiniBusOutboxHostedDispatcher : BackgroundService
{
    private readonly SqlMiniBusOutboxDispatcher _dispatcher;
    private readonly MiniBusSqlHostedOutboxDispatchSettings _settings;
    private readonly ISqlMiniBusOutboxDispatchSignal _signal;
    private readonly ILogger<SqlMiniBusOutboxHostedDispatcher> _logger;

    public SqlMiniBusOutboxHostedDispatcher(
        SqlMiniBusOutboxDispatcher dispatcher,
        MiniBusSqlHostedOutboxDispatchSettings settings,
        ISqlMiniBusOutboxDispatchSignal signal,
        ILogger<SqlMiniBusOutboxHostedDispatcher>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(signal);

        _dispatcher = dispatcher;
        _settings = settings;
        _signal = signal;
        _logger = logger ?? NullLogger<SqlMiniBusOutboxHostedDispatcher>.Instance;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(
            _logger,
            _settings.PollInterval,
            _settings.MaxBatchesPerCycle,
            _settings.FailureBackoff,
            _settings.DrainOnStartup);

        try
        {
            var continueImmediately = false;

            if (_settings.DrainOnStartup)
            {
                LogStartupDrain(_logger);
                continueImmediately = await DispatchCycleWithBackoffAsync(
                        DispatchCycleReasons.Startup,
                        stoppingToken)
                    .ConfigureAwait(false);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                var reason = DispatchCycleReasons.Backlog;

                if (!continueImmediately)
                {
                    var waitResult = await WaitForSignalAsync(stoppingToken).ConfigureAwait(false);

                    if (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (waitResult == DispatchSignalWaitResult.Failed)
                    {
                        reason = DispatchCycleReasons.Poll;
                    }
                    else if (waitResult == DispatchSignalWaitResult.Woken)
                    {
                        LogWakeUp(_logger);
                        reason = DispatchCycleReasons.WakeUp;
                    }
                    else
                    {
                        LogIdlePoll(_logger);
                        reason = DispatchCycleReasons.Poll;
                    }
                }

                continueImmediately = await DispatchCycleWithBackoffAsync(
                        reason,
                        stoppingToken)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            LogStopped(_logger);
        }
    }

    internal async Task<SqlMiniBusHostedOutboxDispatchCycleResult> DispatchCycleAsync(
        CancellationToken cancellationToken = default)
    {
        var batchAttemptCount = 0;
        var claimedCount = 0;
        var dispatchedCount = 0;
        var failedCount = 0;
        var lastBatchClaimedWork = false;

        while (batchAttemptCount < _settings.MaxBatchesPerCycle)
        {
            var result = await _dispatcher
                .DispatchPendingBatchAsync(cancellationToken)
                .ConfigureAwait(false);

            batchAttemptCount++;
            claimedCount += result.ClaimedCount;
            dispatchedCount += result.DispatchedCount;
            failedCount += result.FailedCount;

            lastBatchClaimedWork = result.ClaimedCount > 0;

            if (result.ClaimedCount == 0)
            {
                break;
            }

            if (result.FailedCount > 0)
            {
                return new SqlMiniBusHostedOutboxDispatchCycleResult(
                    batchAttemptCount,
                    claimedCount,
                    dispatchedCount,
                    failedCount,
                    BackoffRequired: true,
                    MoreWorkMayBeAvailable: true);
            }
        }

        return new SqlMiniBusHostedOutboxDispatchCycleResult(
            batchAttemptCount,
            claimedCount,
            dispatchedCount,
            failedCount,
            BackoffRequired: false,
            MoreWorkMayBeAvailable: batchAttemptCount == _settings.MaxBatchesPerCycle && lastBatchClaimedWork);
    }

    private async Task<bool> DispatchCycleWithBackoffAsync(string reason, CancellationToken cancellationToken)
    {
        try
        {
            var result = await DispatchCycleAsync(cancellationToken).ConfigureAwait(false);
            LogCycleCompleted(
                _logger,
                reason,
                result.BatchAttemptCount,
                result.ClaimedCount,
                result.DispatchedCount,
                result.FailedCount);

            if (result.BackoffRequired)
            {
                await BackoffAsync(cancellationToken).ConfigureAwait(false);
            }

            return result.MoreWorkMayBeAvailable;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogCycleFailed(_logger, exception);
            await BackoffAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
    }

    private async Task<DispatchSignalWaitResult> WaitForSignalAsync(CancellationToken cancellationToken)
    {
        try
        {
            var wasWoken = await _signal
                .WaitAsync(_settings.PollInterval, cancellationToken)
                .ConfigureAwait(false);

            return wasWoken
                ? DispatchSignalWaitResult.Woken
                : DispatchSignalWaitResult.TimedOut;
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogSignalWaitFailed(_logger, exception);
            await BackoffAsync(cancellationToken).ConfigureAwait(false);
            return DispatchSignalWaitResult.Failed;
        }
    }

    private async Task BackoffAsync(CancellationToken cancellationToken)
    {
        LogFailureBackoff(_logger, _settings.FailureBackoff);
        await Task.Delay(_settings.FailureBackoff, cancellationToken).ConfigureAwait(false);
    }

    private static class DispatchCycleReasons
    {
        public const string Startup = "startup";
        public const string WakeUp = "wake-up";
        public const string Poll = "poll";
        public const string Backlog = "backlog";
    }

    private enum DispatchSignalWaitResult
    {
        Woken,
        TimedOut,
        Failed
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "SQL outbox hosted dispatcher started with poll interval {PollInterval}, max batches per cycle {MaxBatchesPerCycle}, failure backoff {FailureBackoff}, drain on startup {DrainOnStartup}.")]
    private static partial void LogStarted(
        ILogger logger,
        TimeSpan pollInterval,
        int maxBatchesPerCycle,
        TimeSpan failureBackoff,
        bool drainOnStartup);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "SQL outbox hosted dispatcher is draining pending work on startup.")]
    private static partial void LogStartupDrain(ILogger logger);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Debug,
        Message = "SQL outbox hosted dispatcher woke after a MiniBus-owned SQL commit.")]
    private static partial void LogWakeUp(ILogger logger);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Debug,
        Message = "SQL outbox hosted dispatcher polling after idle interval.")]
    private static partial void LogIdlePoll(ILogger logger);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Debug,
        Message = "SQL outbox hosted dispatcher completed {Reason} cycle with {BatchAttemptCount} batch attempts, {ClaimedCount} claimed, {DispatchedCount} dispatched, and {FailedCount} failed.")]
    private static partial void LogCycleCompleted(
        ILogger logger,
        string reason,
        int batchAttemptCount,
        int claimedCount,
        int dispatchedCount,
        int failedCount);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Warning,
        Message = "SQL outbox hosted dispatcher cycle failed.")]
    private static partial void LogCycleFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Warning,
        Message = "SQL outbox hosted dispatcher backing off for {FailureBackoff}.")]
    private static partial void LogFailureBackoff(ILogger logger, TimeSpan failureBackoff);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Information,
        Message = "SQL outbox hosted dispatcher stopped.")]
    private static partial void LogStopped(ILogger logger);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Warning,
        Message = "SQL outbox hosted dispatcher signal wait failed.")]
    private static partial void LogSignalWaitFailed(ILogger logger, Exception exception);
}
