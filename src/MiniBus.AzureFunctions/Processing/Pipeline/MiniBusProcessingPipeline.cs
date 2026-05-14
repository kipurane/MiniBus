namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal sealed class MiniBusProcessingPipeline
{
    private readonly IReadOnlyList<IMiniBusProcessingBehavior> _behaviors;

    public MiniBusProcessingPipeline(IReadOnlyList<IMiniBusProcessingBehavior> behaviors)
    {
        _behaviors = behaviors;
    }

    public Task InvokeAsync(
        MiniBusProcessingContext context,
        CancellationToken cancellationToken = default)
    {
        return InvokeBehaviorAsync(0, context, cancellationToken);
    }

    private Task InvokeBehaviorAsync(
        int index,
        MiniBusProcessingContext context,
        CancellationToken cancellationToken)
    {
        if (context.IsShortCircuited || index >= _behaviors.Count)
        {
            return Task.CompletedTask;
        }

        return _behaviors[index].InvokeAsync(
            context,
            (nextContext, nextCancellationToken) => InvokeBehaviorAsync(index + 1, nextContext, nextCancellationToken),
            cancellationToken);
    }
}
