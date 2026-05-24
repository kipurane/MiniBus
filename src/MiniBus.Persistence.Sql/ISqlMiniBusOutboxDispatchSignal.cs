namespace MiniBus.Persistence.Sql;

/// <summary>
/// Coordinates in-process wake-ups for the SQL hosted outbox dispatcher.
/// </summary>
/// <remarks>
/// Implementations may coalesce multiple wake-ups into a single signal. Wake-ups are best-effort hints
/// that reduce dispatch latency; correctness must still rely on polling and SQL claim-lease recovery.
/// </remarks>
public interface ISqlMiniBusOutboxDispatchSignal
{
    /// <summary>
    /// Signals that new SQL outbox work may be available.
    /// </summary>
    /// <remarks>
    /// This method must be non-blocking and best-effort. Implementations should not throw for duplicate,
    /// already pending, or otherwise redundant wake-ups.
    /// </remarks>
    void Wake();

    /// <summary>
    /// Waits until the dispatcher is woken, the timeout elapses, or cancellation is requested.
    /// </summary>
    /// <returns>
    /// <see langword="true" /> when a wake-up was observed; <see langword="false" /> when the wait timed out.
    /// </returns>
    /// <remarks>
    /// Implementations must propagate cancellation by throwing an <see cref="OperationCanceledException" />
    /// when <paramref name="cancellationToken" /> is canceled.
    /// </remarks>
    ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken);
}

internal sealed class NoopSqlMiniBusOutboxDispatchSignal : ISqlMiniBusOutboxDispatchSignal
{
    public void Wake()
    {
    }

    public async ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        await Task.Delay(timeout, cancellationToken).ConfigureAwait(false);
        return false;
    }
}

/// <summary>
/// Default coalescing in-process wake-up signal for the SQL hosted outbox dispatcher.
/// </summary>
/// <remarks>
/// The signal stores at most one pending wake-up. Calling <see cref="Wake" /> repeatedly before a waiter
/// observes the signal is treated as one wake-up. The instance is disposable because it owns a
/// <see cref="SemaphoreSlim" />.
/// </remarks>
public sealed class SqlMiniBusOutboxDispatchSignal : ISqlMiniBusOutboxDispatchSignal, IDisposable
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    /// <inheritdoc />
    public void Wake()
    {
        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <inheritdoc />
    public async ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        return await _signal.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Releases resources used by the signal.
    /// </summary>
    public void Dispose()
    {
        _signal.Dispose();
    }
}
