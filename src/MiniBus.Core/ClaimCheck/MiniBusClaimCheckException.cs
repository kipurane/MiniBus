namespace MiniBus.Core.ClaimCheck;

public class MiniBusClaimCheckException : Exception
{
    public MiniBusClaimCheckException(string message)
        : base(message)
    {
    }

    public MiniBusClaimCheckException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
