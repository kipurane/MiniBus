using MiniBus.Core.Context;

namespace MiniBus.Core.Sagas;

public abstract class MiniBusSaga<TData>
    where TData : class, ISagaData, new()
{
    public TData Data { get; private set; } = new();

    public abstract void ConfigureHowToFindSaga(SagaMapper<TData> mapper);

    public void MarkAsComplete()
    {
        Data.IsCompleted = true;
    }

    protected Task RequestTimeout<TTimeout>(
        TTimeout timeout,
        DateTimeOffset dueTime,
        MiniBusContext context,
        CancellationToken cancellationToken = default)
        where TTimeout : ISagaTimeout
    {
        ArgumentNullException.ThrowIfNull(timeout);
        ArgumentNullException.ThrowIfNull(context);

        return context.Schedule(timeout, dueTime, cancellationToken);
    }

    protected Task RequestTimeout<TTimeout>(
        TTimeout timeout,
        TimeSpan delay,
        MiniBusContext context,
        CancellationToken cancellationToken = default)
        where TTimeout : ISagaTimeout
    {
        if (delay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), delay, "Saga timeout delay cannot be negative.");
        }

        return RequestTimeout(timeout, DateTimeOffset.UtcNow.Add(delay), context, cancellationToken);
    }

    protected internal void AttachData(TData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Data = data;
    }
}
