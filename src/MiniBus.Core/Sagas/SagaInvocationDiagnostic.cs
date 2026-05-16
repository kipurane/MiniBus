namespace MiniBus.Core.Sagas;

public sealed record SagaInvocationDiagnostic(
    Type SagaType,
    string CorrelationId,
    bool Completed);
