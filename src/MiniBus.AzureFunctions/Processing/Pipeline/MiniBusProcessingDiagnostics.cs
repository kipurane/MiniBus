using Microsoft.Extensions.Logging;
using MiniBus.Core.Sagas;
using System.Runtime.ExceptionServices;

namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal sealed class MiniBusProcessingDiagnostics
{
    private readonly MiniBusProcessingLogger _logger;
    private readonly MiniBusProcessingTracer _tracer = new();
    private readonly MiniBusProcessingMetrics _metrics = new();

    public MiniBusProcessingDiagnostics(ILoggerFactory? loggerFactory)
    {
        _logger = new MiniBusProcessingLogger(loggerFactory);
    }

    public IDisposable BeginAttempt(MiniBusProcessingContext context)
    {
        var activity = _tracer.StartProcessingActivity(context);
        IDisposable? scope = null;
        try
        {
            context.ProcessingMetricAttempt = _metrics.StartProcessingAttempt();
            scope = _logger.BeginProcessingScope(context);
            _logger.ProcessingStarted(context);
            return new ProcessingAttemptScope(scope, activity);
        }
        catch (Exception startupException)
        {
            try
            {
                new ProcessingAttemptScope(scope, activity).Dispose();
            }
            catch (Exception cleanupException)
            {
                throw new AggregateException("MiniBus diagnostics startup and cleanup failed.", startupException, cleanupException);
            }

            throw;
        }
    }

    public void ProcessingSucceeded(MiniBusProcessingContext context)
    {
        if (context.IsShortCircuited)
        {
            _logger.ProcessingSkippedDuplicate(context);
            _tracer.ProcessingSkippedDuplicate(context);
            _metrics.ProcessingSkippedDuplicate(context);
            return;
        }

        _logger.ProcessingCompleted(context);
        _tracer.ProcessingCompleted(context);
        _metrics.ProcessingCompleted(context);
    }

    public void ProcessingRetried(MiniBusProcessingContext context, Exception exception)
    {
        _logger.ProcessingRetried(context, exception);
        _tracer.ProcessingRetried(context, exception);
        _metrics.ProcessingRetried(context);
    }

    public void ProcessingFailed(MiniBusProcessingContext context, Exception exception)
    {
        _logger.ProcessingFailed(context, exception);
        _tracer.ProcessingFailed(context, exception);
        _metrics.ProcessingFailed(context);
    }

    public void ProcessingDelayedRetryScheduled(MiniBusProcessingContext context)
    {
        _logger.ProcessingDelayedRetryScheduled(context);
        _tracer.ProcessingDelayedRetryScheduled(context);
        _metrics.ProcessingDelayedRetryScheduled(context);
    }

    public void ProcessingDeadLettered(MiniBusProcessingContext context, string? reason, string? description)
    {
        _logger.ProcessingDeadLettered(context, reason, description);
        _tracer.ProcessingDeadLettered(context, reason, description);
        _metrics.ProcessingDeadLettered(context);
    }

    public void OutboxCommitted(MiniBusProcessingContext context)
    {
        _logger.OutboxCommitted(context);
        _tracer.OutboxCommitted(context);
    }

    public Task ObserveHandlerInvocationAsync(
        MiniBusProcessingContext context,
        Type handlerType,
        Func<Task> invoke)
    {
        context.LastHandlerType = handlerType;
        if (_logger.IsHandlerInvocationEnabled())
        {
            _logger.HandlerInvoked(context, handlerType);
        }

        if (context.ProcessingActivity is not null)
        {
            _tracer.HandlerInvoked(context, handlerType);
        }
        return _metrics.MeasureHandlerInvocationAsync(context, handlerType, invoke);
    }

    public async Task<SagaInvocationDiagnostic> ObserveSagaInvocationAsync(
        MiniBusProcessingContext context,
        Type sagaType,
        Func<Task<SagaInvocationDiagnostic>> invoke)
    {
        var diagnostic = await _metrics.MeasureSagaInvocationAsync(context, sagaType, invoke).ConfigureAwait(false);
        context.LastSagaType = diagnostic.SagaType;
        context.LastSagaCorrelationId = diagnostic.CorrelationId;
        _logger.SagaInvoked(context, diagnostic.SagaType, diagnostic.CorrelationId);
        _tracer.SagaInvoked(context, diagnostic.SagaType, diagnostic.CorrelationId);
        if (diagnostic.Completed)
        {
            _logger.SagaCompleted(context, diagnostic.SagaType, diagnostic.CorrelationId);
            _tracer.SagaCompleted(context, diagnostic.SagaType, diagnostic.CorrelationId);
        }

        return diagnostic;
    }

    private sealed class ProcessingAttemptScope : IDisposable
    {
        private readonly IDisposable? _loggingScope;
        private readonly IDisposable? _activity;

        public ProcessingAttemptScope(
            IDisposable? loggingScope,
            IDisposable? activity)
        {
            _loggingScope = loggingScope;
            _activity = activity;
        }

        public void Dispose()
        {
            // Always attempt both disposals; if both fail, surface both exceptions to avoid hiding either provider failure.
            Exception? loggingScopeException = null;
            try
            {
                _loggingScope?.Dispose();
            }
            catch (Exception exception)
            {
                loggingScopeException = exception;
            }

            try
            {
                _activity?.Dispose();
            }
            catch (Exception activityException) when (loggingScopeException is not null)
            {
                throw new AggregateException(
                    "MiniBus processing scope disposal failed.",
                    loggingScopeException,
                    activityException);
            }

            if (loggingScopeException is not null)
            {
                ExceptionDispatchInfo.Capture(loggingScopeException).Throw();
            }
        }
    }
}
