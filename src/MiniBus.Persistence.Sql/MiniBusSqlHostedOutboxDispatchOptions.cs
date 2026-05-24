namespace MiniBus.Persistence.Sql;

public sealed class MiniBusSqlHostedOutboxDispatchOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    public int MaxBatchesPerCycle { get; set; } = 10;

    public TimeSpan FailureBackoff { get; set; } = TimeSpan.FromSeconds(30);

    public bool DrainOnStartup { get; set; } = true;

    internal MiniBusSqlHostedOutboxDispatchSettings ToSettings()
    {
        Validate();
        return new MiniBusSqlHostedOutboxDispatchSettings(
            PollInterval,
            MaxBatchesPerCycle,
            FailureBackoff,
            DrainOnStartup);
    }

    internal void Validate()
    {
        if (PollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(PollInterval),
                PollInterval,
                "The SQL hosted outbox dispatch poll interval must be greater than zero.");
        }

        if (MaxBatchesPerCycle <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxBatchesPerCycle),
                MaxBatchesPerCycle,
                "The SQL hosted outbox dispatch maximum batches per cycle must be greater than zero.");
        }

        if (FailureBackoff <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(FailureBackoff),
                FailureBackoff,
                "The SQL hosted outbox dispatch failure backoff must be greater than zero.");
        }
    }
}

internal sealed record MiniBusSqlHostedOutboxDispatchSettings(
    TimeSpan PollInterval,
    int MaxBatchesPerCycle,
    TimeSpan FailureBackoff,
    bool DrainOnStartup);
