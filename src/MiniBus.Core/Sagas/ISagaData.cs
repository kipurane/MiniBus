namespace MiniBus.Core.Sagas;

public interface ISagaData
{
    Guid Id { get; set; }

    string CorrelationId { get; set; }

    bool IsCompleted { get; set; }
}
