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

    protected internal void AttachData(TData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Data = data;
    }
}
