namespace MiniBus.Core.ClaimCheck;

public sealed class MiniBusInvalidClaimCheckReferenceException : MiniBusClaimCheckException
{
    public MiniBusInvalidClaimCheckReferenceException(string message)
        : base(message)
    {
    }
}
