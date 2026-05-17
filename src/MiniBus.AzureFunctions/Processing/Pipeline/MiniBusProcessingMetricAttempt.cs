namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal sealed class MiniBusProcessingMetricAttempt
{
    public MiniBusProcessingMetricAttempt(long startTimestamp)
    {
        StartTimestamp = startTimestamp;
    }

    public long StartTimestamp { get; }
}
