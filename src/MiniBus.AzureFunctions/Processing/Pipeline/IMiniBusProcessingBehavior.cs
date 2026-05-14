namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal delegate Task MiniBusProcessingDelegate(
    MiniBusProcessingContext context,
    CancellationToken cancellationToken);

internal interface IMiniBusProcessingBehavior
{
    Task InvokeAsync(
        MiniBusProcessingContext context,
        MiniBusProcessingDelegate next,
        CancellationToken cancellationToken);
}
