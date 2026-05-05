namespace MiniBus.AzureFunctions.Processing;

public sealed class MiniBusMessageTypeResolutionException : InvalidOperationException
{
    public MiniBusMessageTypeResolutionException(string message)
        : base(message)
    {
    }
}
