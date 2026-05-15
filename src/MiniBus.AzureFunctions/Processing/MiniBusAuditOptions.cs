namespace MiniBus.AzureFunctions.Processing;

public sealed class MiniBusAuditOptions
{
    public bool CaptureClaimCheckedBodies { get; set; }

    public Func<string> AuditIdFactory { get; set; } = () => Guid.NewGuid().ToString("N");

    public Func<DateTimeOffset> UtcNowProvider { get; set; } = () => DateTimeOffset.UtcNow;

    public void Validate()
    {
        if (AuditIdFactory is null)
        {
            throw new InvalidOperationException("MiniBus audit AuditIdFactory cannot be null.");
        }

        if (UtcNowProvider is null)
        {
            throw new InvalidOperationException("MiniBus audit UtcNowProvider cannot be null.");
        }
    }
}
