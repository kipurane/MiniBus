namespace MiniBus.Core.ClaimCheck;

public sealed class MiniBusClaimCheckConfigurationException : MiniBusClaimCheckException
{
    public MiniBusClaimCheckConfigurationException(string message)
        : base(message)
    {
    }
}
