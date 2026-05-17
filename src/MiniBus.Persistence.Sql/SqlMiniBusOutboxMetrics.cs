using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Reflection;
using MiniBus.Core.Persistence;

namespace MiniBus.Persistence.Sql;

internal sealed class SqlMiniBusOutboxMetrics
{
    // Stable Meter used by applications in AddMeter("MiniBus.Persistence.Sql").
    // Changing this should be treated as an observability contract change.
    public const string MeterName = "MiniBus.Persistence.Sql";

    // Stable instrument names, units, descriptions, and tag names are observability contracts.
    public const string DispatchBatchesInstrumentName = "minibus.sql_outbox.dispatch.batches";
    public const string DispatchBatchDurationInstrumentName = "minibus.sql_outbox.dispatch.batch_duration";
    public const string DispatchOperationsInstrumentName = "minibus.sql_outbox.dispatch.operations";
    public const string DispatchOperationDurationInstrumentName = "minibus.sql_outbox.dispatch.operation_duration";

    public const string BatchUnit = "{batch}";
    public const string OperationUnit = "{operation}";
    public const string DurationUnit = "s";

    [SuppressMessage(
        "Usage",
        "CA2213:Disposable fields should be disposed",
        Justification = "The shared Meter is process-lifetime diagnostic infrastructure and must not be disposed by individual dispatchers.")]
    private static readonly string MeterVersion = GetMeterVersion();

    private static readonly Meter Meter = new(MeterName, MeterVersion);

    private static readonly Counter<long> DispatchBatches = Meter.CreateCounter<long>(
        DispatchBatchesInstrumentName,
        BatchUnit,
        "Number of MiniBus SQL outbox dispatch batches.");

    private static readonly Histogram<double> DispatchBatchDuration = Meter.CreateHistogram<double>(
        DispatchBatchDurationInstrumentName,
        DurationUnit,
        "Duration of MiniBus SQL outbox dispatch batches.");

    private static readonly Counter<long> DispatchOperations = Meter.CreateCounter<long>(
        DispatchOperationsInstrumentName,
        OperationUnit,
        "Number of MiniBus SQL outbox dispatch operations.");

    private static readonly Histogram<double> DispatchOperationDuration = Meter.CreateHistogram<double>(
        DispatchOperationDurationInstrumentName,
        DurationUnit,
        "Duration of MiniBus SQL outbox dispatch operations.");

    public SqlMiniBusOutboxMetricScope? StartBatch()
    {
        if (!DispatchBatches.Enabled && !DispatchBatchDuration.Enabled)
        {
            return null;
        }

        return new SqlMiniBusOutboxMetricScope(Stopwatch.GetTimestamp());
    }

    public SqlMiniBusOutboxMetricScope? StartOperation()
    {
        if (!DispatchOperations.Enabled && !DispatchOperationDuration.Enabled)
        {
            return null;
        }

        return new SqlMiniBusOutboxMetricScope(Stopwatch.GetTimestamp());
    }

    public void RecordBatch(
        SqlMiniBusOutboxMetricScope? scope,
        int claimedCount,
        int dispatchedCount,
        int failedCount,
        string outcome)
    {
        if (DispatchBatches.Enabled)
        {
            DispatchBatches.Add(1, CreateDispatchTags(outcome));
        }

        RecordOperationCount(claimedCount, "claimed");
        RecordOperationCount(dispatchedCount, SqlMiniBusOutboxDispatchOutcomes.Succeeded);
        RecordOperationCount(failedCount, SqlMiniBusOutboxDispatchOutcomes.Failed);

        if (DispatchBatchDuration.Enabled && scope is not null)
        {
            DispatchBatchDuration.Record(GetElapsedSeconds(scope.StartTimestamp), CreateDispatchTags(outcome));
        }
    }

    public void RecordOperation(
        MiniBusOutboxStoredOperation operation,
        SqlMiniBusOutboxMetricScope? scope,
        string outcome)
    {
        if (DispatchOperations.Enabled)
        {
            DispatchOperations.Add(1, CreateOperationTags(operation.Kind, outcome));
        }

        if (DispatchOperationDuration.Enabled && scope is not null)
        {
            DispatchOperationDuration.Record(
                GetElapsedSeconds(scope.StartTimestamp),
                CreateOperationTags(operation.Kind, outcome));
        }
    }

    private static void RecordOperationCount(
        int count,
        string outcome)
    {
        if (count > 0 && DispatchOperations.Enabled)
        {
            DispatchOperations.Add(count, CreateDispatchTags(outcome));
        }
    }

    private static TagList CreateOperationTags(
        MiniBusOutboxOperationKind operationKind,
        string outcome)
    {
        var tags = CreateDispatchTags(outcome);
        tags.Add(SqlMiniBusOutboxMetricTags.MiniBusOutboxOperationKind, operationKind.ToString());
        return tags;
    }

    private static TagList CreateDispatchTags(string outcome)
    {
        var tags = new TagList
        {
            { SqlMiniBusOutboxMetricTags.MiniBusSqlOutboxDispatchOutcome, outcome }
        };
        return tags;
    }

    private static double GetElapsedSeconds(long startTimestamp)
    {
        return Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
    }

    private static string GetMeterVersion()
    {
        try
        {
            var assembly = typeof(SqlMiniBusOutboxMetrics).Assembly;
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informationalVersion))
            {
                return informationalVersion;
            }

            return assembly.GetName().Version?.ToString() ?? "0.0.0";
        }
        catch
        {
            return "0.0.0";
        }
    }
}
