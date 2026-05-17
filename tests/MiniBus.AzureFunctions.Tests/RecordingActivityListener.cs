using System.Collections.Concurrent;
using System.Diagnostics;
using MiniBus.AzureFunctions.Processing.Pipeline;

namespace MiniBus.AzureFunctions.Tests;

internal sealed class RecordingActivityListener : IDisposable
{
    private readonly ActivityListener _listener;

    public RecordingActivityListener()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => string.Equals(source.Name, MiniBusProcessingTracer.SourceName, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => Activities.Enqueue(activity)
        };

        ActivitySource.AddActivityListener(_listener);
    }

    public ConcurrentQueue<Activity> Activities { get; } = new();

    public void Dispose()
    {
        _listener.Dispose();
    }
}