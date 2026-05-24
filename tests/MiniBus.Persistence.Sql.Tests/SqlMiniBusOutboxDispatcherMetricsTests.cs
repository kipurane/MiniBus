using System.Diagnostics.Metrics;
using MiniBus.Core.Contracts;
using MiniBus.Core.Persistence;
using Xunit;

namespace MiniBus.Persistence.Sql.Tests;

public sealed class SqlMiniBusOutboxDispatcherMetricsTests
{
    [Fact]
    public async Task DispatchPendingAsync_EmitsBatchAndOperationMetrics()
    {
        using var metrics = new RecordingMeterListener(SqlMiniBusOutboxMetrics.MeterName);
        var operations = new[]
        {
            CreateOperation(MiniBusOutboxOperationKind.Send),
            CreateOperation(MiniBusOutboxOperationKind.Publish)
        };
        var store = new RecordingOutboxStore(operations);
        var dispatcher = new RecordingOutboxDispatcher
        {
            FailingOperationId = operations[1].Id
        };
        var outboxDispatcher = new SqlMiniBusOutboxDispatcher(
            store,
            dispatcher,
            new MiniBusSqlPersistenceOptions { DispatcherBatchSize = 10 },
            new SqlMiniBusOutboxMetrics());

        var dispatched = await outboxDispatcher.DispatchPendingAsync();

        Assert.Equal(1, dispatched);
        Assert.Equal(10, store.BatchSize);
        Assert.Contains(operations[0].Id, store.Dispatched);
        Assert.Contains(operations[1].Id, store.Failed);

        var batch = Assert.Single(metrics.Measurements, measurement =>
            measurement.InstrumentName == SqlMiniBusOutboxMetrics.DispatchBatchesInstrumentName);
        Assert.Equal(1L, batch.Value);
        Assert.Equal(SqlMiniBusOutboxMetrics.MeterName, batch.MeterName);
        Assert.Equal(SqlMiniBusOutboxMetrics.BatchUnit, batch.Unit);

        var batchDuration = Assert.Single(metrics.Measurements, measurement =>
            measurement.InstrumentName == SqlMiniBusOutboxMetrics.DispatchBatchDurationInstrumentName);
        Assert.Equal(SqlMiniBusOutboxMetrics.DurationUnit, batchDuration.Unit);
        Assert.True((double)batchDuration.Value >= 0);

        Assert.Contains(metrics.Measurements, measurement =>
            measurement.InstrumentName == SqlMiniBusOutboxMetrics.DispatchOperationsInstrumentName
            && Equals(measurement.Value, 2L)
            && (string?)measurement.Tags[SqlMiniBusOutboxMetricTags.MiniBusSqlOutboxDispatchOutcome] == "claimed");
        Assert.Contains(metrics.Measurements, measurement =>
            measurement.InstrumentName == SqlMiniBusOutboxMetrics.DispatchOperationsInstrumentName
            && Equals(measurement.Value, 1L)
            && (string?)measurement.Tags[SqlMiniBusOutboxMetricTags.MiniBusSqlOutboxDispatchOutcome] == SqlMiniBusOutboxDispatchOutcomes.Succeeded);
        Assert.Contains(metrics.Measurements, measurement =>
            measurement.InstrumentName == SqlMiniBusOutboxMetrics.DispatchOperationsInstrumentName
            && Equals(measurement.Value, 1L)
            && (string?)measurement.Tags[SqlMiniBusOutboxMetricTags.MiniBusSqlOutboxDispatchOutcome] == SqlMiniBusOutboxDispatchOutcomes.Failed);

        var operationDurations = metrics.Measurements
            .Where(measurement => measurement.InstrumentName == SqlMiniBusOutboxMetrics.DispatchOperationDurationInstrumentName)
            .ToArray();
        Assert.Equal(2, operationDurations.Length);
        Assert.Contains(operationDurations, measurement =>
            (string?)measurement.Tags[SqlMiniBusOutboxMetricTags.MiniBusOutboxOperationKind] == MiniBusOutboxOperationKind.Send.ToString()
            && (string?)measurement.Tags[SqlMiniBusOutboxMetricTags.MiniBusSqlOutboxDispatchOutcome] == SqlMiniBusOutboxDispatchOutcomes.Succeeded);
        Assert.Contains(operationDurations, measurement =>
            (string?)measurement.Tags[SqlMiniBusOutboxMetricTags.MiniBusOutboxOperationKind] == MiniBusOutboxOperationKind.Publish.ToString()
            && (string?)measurement.Tags[SqlMiniBusOutboxMetricTags.MiniBusSqlOutboxDispatchOutcome] == SqlMiniBusOutboxDispatchOutcomes.Failed);
        Assert.All(operationDurations, measurement =>
        {
            Assert.False(measurement.Tags.ContainsKey("minibus.sql_outbox.row_id"));
            Assert.False(measurement.Tags.ContainsKey("minibus.outgoing_message_id"));
        });
    }

    [Fact]
    public async Task DispatchPendingAsync_CompletesWithoutMetricsListener()
    {
        var operation = CreateOperation(MiniBusOutboxOperationKind.Send);
        var store = new RecordingOutboxStore(new[] { operation });
        var dispatcher = new RecordingOutboxDispatcher();
        var outboxDispatcher = new SqlMiniBusOutboxDispatcher(
            store,
            dispatcher,
            new MiniBusSqlPersistenceOptions { DispatcherBatchSize = 10 },
            new SqlMiniBusOutboxMetrics());

        var dispatched = await outboxDispatcher.DispatchPendingAsync();

        Assert.Equal(1, dispatched);
        Assert.Contains(operation.Id, store.Dispatched);
    }

    private static MiniBusOutboxStoredOperation CreateOperation(MiniBusOutboxOperationKind kind)
    {
        return new MiniBusOutboxStoredOperation(
            Guid.NewGuid(),
            $"outgoing-{Guid.NewGuid():N}",
            kind,
            BinaryData.FromString("{}"),
            typeof(TestCommand),
            new Dictionary<string, string>(StringComparer.Ordinal),
            DueTime: null,
            AttemptCount: 1);
    }

    private sealed record TestCommand(Guid Id) : ICommand;

    private sealed class RecordingOutboxStore : ISqlMiniBusOutboxStore
    {
        private readonly IReadOnlyList<MiniBusOutboxStoredOperation> _operations;

        public RecordingOutboxStore(IReadOnlyList<MiniBusOutboxStoredOperation> operations)
        {
            _operations = operations;
        }

        public int? BatchSize { get; private set; }

        public List<Guid> Dispatched { get; } = new();

        public List<Guid> Failed { get; } = new();

        public Task<IReadOnlyList<MiniBusOutboxStoredOperation>> ClaimPendingAsync(
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            BatchSize = batchSize;
            return Task.FromResult<IReadOnlyList<MiniBusOutboxStoredOperation>>(
                _operations
                    .Where(operation => !Dispatched.Contains(operation.Id))
                    .ToArray());
        }

        public Task MarkDispatchedAsync(
            Guid operationId,
            CancellationToken cancellationToken = default)
        {
            Dispatched.Add(operationId);
            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(
            Guid operationId,
            Exception exception,
            CancellationToken cancellationToken = default)
        {
            Failed.Add(operationId);
            return Task.CompletedTask;
        }

        public Task<int> CleanupAsync(CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class RecordingOutboxDispatcher : IMiniBusOutboxDispatcher
    {
        public Guid? FailingOperationId { get; init; }

        public Task DispatchAsync(
            MiniBusOutboxStoredOperation operation,
            CancellationToken cancellationToken = default)
        {
            if (operation.Id == FailingOperationId)
            {
                return Task.FromException(new InvalidOperationException("dispatch failed"));
            }

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingMeterListener : IDisposable
    {
        private readonly HashSet<string> _meterNames;
        private readonly MeterListener _listener = new();

        public RecordingMeterListener(params string[] meterNames)
        {
            _meterNames = new HashSet<string>(meterNames, StringComparer.Ordinal);
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (_meterNames.Contains(instrument.Meter.Name))
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
                Measurements.Add(RecordedMeasurement.Create(instrument, measurement, tags)));
            _listener.SetMeasurementEventCallback<double>((instrument, measurement, tags, _) =>
                Measurements.Add(RecordedMeasurement.Create(instrument, measurement, tags)));
            _listener.Start();
        }

        public List<RecordedMeasurement> Measurements { get; } = new();

        public void Dispose()
        {
            _listener.Dispose();
        }
    }

    private sealed record RecordedMeasurement(
        string MeterName,
        string InstrumentName,
        string? Unit,
        object Value,
        IReadOnlyDictionary<string, object?> Tags)
    {
        public static RecordedMeasurement Create<T>(
            Instrument instrument,
            T value,
            ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            return new RecordedMeasurement(
                instrument.Meter.Name,
                instrument.Name,
                instrument.Unit,
                value!,
                tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.Ordinal));
        }
    }
}
