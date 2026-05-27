namespace MiniBus.Tooling.Core;

public sealed record MiniBusToolingValidationResult(bool IsValid, string? Error)
{
    public static MiniBusToolingValidationResult Valid { get; } = new(true, null);

    public static MiniBusToolingValidationResult Invalid(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new MiniBusToolingValidationResult(false, error);
    }
}
