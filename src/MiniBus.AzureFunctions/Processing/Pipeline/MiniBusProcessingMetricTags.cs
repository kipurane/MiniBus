namespace MiniBus.AzureFunctions.Processing.Pipeline;

internal static class MiniBusProcessingMetricTags
{
    // Stable metric tag names. Changing these should be treated as an observability contract change.
    public const string MiniBusEndpoint = "minibus.endpoint";
    public const string MiniBusMessageType = "minibus.message_type";
    public const string MiniBusProcessingOutcome = "minibus.processing_outcome";
    public const string MiniBusRetryKind = "minibus.retry_kind";
    public const string MiniBusHandlerType = "minibus.handler_type";
    public const string MiniBusHandlerOutcome = "minibus.handler_outcome";
    public const string MiniBusSagaType = "minibus.saga_type";
    public const string MiniBusSagaOutcome = "minibus.saga_outcome";
}
