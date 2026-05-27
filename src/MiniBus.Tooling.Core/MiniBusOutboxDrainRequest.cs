namespace MiniBus.Tooling.Core;

public sealed record MiniBusOutboxDrainRequest(int MaxBatches)
{
    public MiniBusToolingValidationResult Validate()
    {
        return MaxBatches > 0
            ? MiniBusToolingValidationResult.Valid
            : MiniBusToolingValidationResult.Invalid("MaxBatches must be greater than zero.");
    }
}
