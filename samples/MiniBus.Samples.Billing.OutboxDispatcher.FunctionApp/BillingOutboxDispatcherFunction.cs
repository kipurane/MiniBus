using System.Globalization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MiniBus.Persistence.Sql;

namespace MiniBus.Samples.Billing.OutboxDispatcher.FunctionApp;

public sealed partial class BillingOutboxDispatcherFunction
{
    public const string ScheduleSetting = "BillingOutboxDispatchSchedule";
    public const string MaxBatchesSetting = "BillingOutboxDispatchMaxBatches";
    public const int DefaultMaxBatches = 5;
    internal const int OutboxDrainCompletedEventId = 1001;

    private readonly Func<int, CancellationToken, Task<int>> _dispatchPendingBatchesAsync;
    private readonly int _maxBatches;
    private readonly ILogger _logger;

    public BillingOutboxDispatcherFunction(
        SqlMiniBusOutboxDispatcher dispatcher,
        IConfiguration configuration,
        ILogger<BillingOutboxDispatcherFunction> logger)
        : this(
            dispatcher.DispatchPendingBatchesAsync,
            GetMaxBatches(configuration),
            logger)
    {
    }

    internal BillingOutboxDispatcherFunction(
        Func<int, CancellationToken, Task<int>> dispatchPendingBatchesAsync,
        int maxBatches,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(dispatchPendingBatchesAsync);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBatches);
        ArgumentNullException.ThrowIfNull(logger);

        _dispatchPendingBatchesAsync = dispatchPendingBatchesAsync;
        _maxBatches = maxBatches;
        _logger = logger;
    }

    [Function("BillingOutboxDispatcher")]
    public async Task Run(
        [TimerTrigger("%" + ScheduleSetting + "%")] TimerInfo timerInfo,
        CancellationToken cancellationToken)
    {
        var dispatched = await DispatchPendingAsync(cancellationToken).ConfigureAwait(false);
        LogDrainCompleted(_logger, dispatched, _maxBatches, timerInfo.IsPastDue);
    }

    internal Task<int> DispatchPendingAsync(CancellationToken cancellationToken = default)
    {
        return _dispatchPendingBatchesAsync(_maxBatches, cancellationToken);
    }

    internal static int GetMaxBatches(IConfiguration configuration)
    {
        var configuredValue = configuration[MaxBatchesSetting];
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return DefaultMaxBatches;
        }

        if (int.TryParse(
                configuredValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var maxBatches)
            && maxBatches > 0)
        {
            return maxBatches;
        }

        throw new InvalidOperationException(
            $"Set '{MaxBatchesSetting}' to a positive integer when configuring the Billing outbox dispatcher. " +
            $"The configured value was '{configuredValue}'.");
    }

    [LoggerMessage(
        EventId = OutboxDrainCompletedEventId,
        Level = LogLevel.Information,
        Message = "Billing outbox dispatcher drained {dispatchedOperationCount} pending operations with a maximum of {maxBatches} batches. IsPastDue: {isPastDue}.")]
    private static partial void LogDrainCompleted(
        ILogger logger,
        int dispatchedOperationCount,
        int maxBatches,
        bool isPastDue);
}
