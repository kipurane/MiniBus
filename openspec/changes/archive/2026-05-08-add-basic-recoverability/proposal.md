## Why

MiniBus can process Azure Service Bus trigger messages through Azure Functions and can dispatch scheduled Service Bus messages, but failures are currently handled as immediate unrecoverable dead-letter events. A transient handler failure therefore skips the predictable retry path developers expect from a message-processing framework.

This change adds basic recoverability so a failing handler can be retried immediately in the same Functions invocation, then rescheduled as a delayed retry through Azure Service Bus, and finally dead-lettered only after the configured policy is exhausted.

## What Changes

- Add transport-independent recoverability options, retry metadata, and a recoverability decision model to `MiniBus.Core`.
- Add immediate retry orchestration to Azure Functions processing without creating new Service Bus messages.
- Add delayed retry scheduling by creating a scheduled copy of the original Service Bus message through the Azure Service Bus transport layer.
- Add MiniBus retry headers for immediate attempts, delayed attempts, configured retry limits, original message id, and exception details.
- Preserve original message id and correlation headers across immediate and delayed retries.
- Dead-letter only when the configured recoverability policy is exhausted and dead-lettering is enabled.
- Provide useful dead-letter reason and description values that include failure and retry context.
- Add tests for immediate retry behavior, delayed retry scheduling decisions, and retries-exhausted dead-letter behavior.
- Add documentation and sample configuration for the default recoverability shape.

## Capabilities

### New Capabilities
- `basic-recoverability`: Basic retry and dead-letter behavior for MiniBus message processing in Azure Functions with Azure Service Bus.

### Modified Capabilities
- `minibus-core`: Adds transport-independent recoverability configuration, retry headers, and decision contracts.
- `azure-servicebus-transport`: Adds Service Bus-specific support for creating scheduled retry copies while preserving MiniBus headers.
- `azure-functions-adapter`: Applies recoverability decisions during inbound processing and settlement.

## Impact

- Affected code: `src/MiniBus.Core`, `src/MiniBus.AzureServiceBus`, `src/MiniBus.AzureFunctions`, corresponding test projects, and Azure Functions README/sample configuration.
- Public APIs: introduces recoverability options such as `ImmediateRetries`, `DelayedRetries`, and `DeadLetterAfterRetriesExhausted`; exposes transport-independent decision and header concepts from core.
- Dependencies: no new infrastructure dependencies beyond existing Azure Service Bus and Azure Functions packages.
- Systems: no SQL inbox, SQL outbox, saga persistence, Service Bus sessions, dashboard, manual retry tooling, advanced exception classification, OpenTelemetry metrics, or source-generated Function wrappers.
