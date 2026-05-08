namespace MiniBus.Core.Recoverability;

public static class MiniBusRecoverabilityHeaderNames
{
    public const string ImmediateAttempt = "MiniBus.Retry.ImmediateAttempt";
    public const string DelayedAttempt = "MiniBus.Retry.DelayedAttempt";
    public const string MaxImmediateAttempts = "MiniBus.Retry.MaxImmediateAttempts";
    public const string MaxDelayedAttempts = "MiniBus.Retry.MaxDelayedAttempts";
    public const string OriginalMessageId = "MiniBus.OriginalMessageId";
    public const string ExceptionType = "MiniBus.Exception.Type";
    public const string ExceptionMessage = "MiniBus.Exception.Message";
}
