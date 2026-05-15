using Microsoft.Extensions.DependencyInjection;
using MiniBus.Core.ClaimCheck;

namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal sealed class ClaimCheckPayloadResolutionBehavior : IMiniBusProcessingBehavior
{
    private readonly IServiceProvider _serviceProvider;

    public ClaimCheckPayloadResolutionBehavior(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task InvokeAsync(
        MiniBusProcessingContext context,
        MiniBusProcessingDelegate next,
        CancellationToken cancellationToken)
    {
        if (!MiniBusClaimCheckReferenceReader.IsClaimChecked(context.Headers))
        {
            await next(context, cancellationToken).ConfigureAwait(false);
            return;
        }

        var store = _serviceProvider.GetService<IMiniBusClaimCheckPayloadStore>();
        if (store is null)
        {
            throw new MiniBusClaimCheckConfigurationException(
                "MiniBus received a claim-checked message, but no claim-check payload store is configured. "
                + "Register an IMiniBusClaimCheckPayloadStore implementation using AddMiniBusAzureStoragePersistence or a custom provider.");
        }

        var reference = MiniBusClaimCheckReferenceReader.Read(context.Headers);
        context.Body = await store.ReadAsync(reference, cancellationToken).ConfigureAwait(false);

        await next(context, cancellationToken).ConfigureAwait(false);
    }
}
