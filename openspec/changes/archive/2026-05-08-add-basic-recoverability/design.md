## Context

MiniBus already has a transport-agnostic core with message contracts, context, headers, serialization, and handler invocation. `MiniBus.AzureServiceBus` can create outgoing `ServiceBusMessage` envelopes, map MiniBus headers to application properties, and schedule messages. `MiniBus.AzureFunctions` owns inbound Service Bus trigger processing through `MiniBusProcessor`, including basic completion and dead-letter settlement.

The next step is to replace "any processing exception dead-letters immediately" with a predictable recoverability policy. The policy must remain conceptually transport-independent in core while allowing Azure Service Bus scheduled messages to implement delayed retries.

## Goals / Non-Goals

**Goals:**
- Add a small recoverability configuration model with immediate retry count, delayed retry delays, and dead-letter-after-exhaustion behavior.
- Add a core recoverability decision model that can decide between immediate retry, delayed retry, and dead-letter.
- Store all retry metadata in MiniBus headers.
- Run immediate retries inside the same `MiniBusProcessor` invocation without creating new Service Bus messages.
- Schedule delayed retries by creating a scheduled copy of the original received Service Bus message.
- Preserve correlation headers and store `MiniBus.OriginalMessageId` so delayed retry messages remain tied to the first transport message.
- Preserve original exception information when handler invocation fails and when dead-lettering.
- Dead-letter only after immediate and delayed retry policies are exhausted.
- Keep delayed retry scheduling Service Bus-specific and outside `MiniBus.Core`.
- Add focused unit tests and README/sample configuration.

**Non-Goals:**
- SQL inbox or SQL outbox.
- Saga persistence.
- Service Bus sessions.
- Poison-message dashboard or manual retry tooling.
- Advanced exception classification beyond "processing failed".
- OpenTelemetry metrics.
- Source-generated Azure Function wrappers.

## Decisions

### 1. Put policy and decisions in core, execution in adapters

`MiniBus.Core` will define recoverability options, retry header names, and decision types such as immediate retry, delayed retry, and dead-letter. Core will not reference Azure Service Bus, Azure Functions, or scheduled-message APIs.

**Rationale:** Recoverability concepts are framework-level behavior, but delayed retry mechanics are transport-specific. This keeps core reusable while allowing the Functions and Service Bus packages to execute decisions.

**Alternatives considered:**
- Put all recoverability in `MiniBus.AzureFunctions`: rejected because retry headers and policy decisions would become host-specific.
- Put scheduling in `MiniBus.Core`: rejected because Service Bus scheduled messages are transport-specific.

### 2. Immediate retries stay inside the current processing invocation

When a handler or outgoing operation fails, `MiniBusProcessor` will consult the recoverability policy and, if an immediate retry remains, invoke the processing flow again with incremented immediate retry metadata. The same received Service Bus message remains unsettled until processing succeeds or a later decision is executed.

**Rationale:** Immediate retries are meant to handle short-lived in-memory failures without adding transport traffic or extra queue latency.

**Alternatives considered:**
- Abandon the message for immediate retries: rejected because it delegates timing to Service Bus delivery behavior and loses the "same invocation" guarantee.
- Schedule every retry as a new message: rejected because immediate retries must not create new Service Bus messages.

### 3. Delayed retries are scheduled copies of the original transport message

When immediate retries are exhausted and a delayed retry remains, the Functions adapter will ask a Service Bus-specific retry scheduler to create a scheduled `ServiceBusMessage` copy from the original `ServiceBusReceivedMessage`. The copy will preserve body, message type headers, correlation headers, content type, and application properties, while updating retry and exception headers. The current received message will be completed after the scheduled retry is accepted.

**Rationale:** Azure Service Bus scheduled messages provide durable delayed delivery without requiring an inbox/outbox in this change. Completing the failed original after scheduling prevents duplicate retry paths.

**Alternatives considered:**
- Defer the original message: rejected because Service Bus deferral requires sequence-number retrieval and does not behave like a simple delayed retry queue.
- Abandon and rely on lock redelivery: rejected because it cannot express configured delayed intervals.

### 4. Retry attempts are represented by MiniBus headers

The retry flow will use these headers:

- `MiniBus.Retry.ImmediateAttempt`
- `MiniBus.Retry.DelayedAttempt`
- `MiniBus.Retry.MaxImmediateAttempts`
- `MiniBus.Retry.MaxDelayedAttempts`
- `MiniBus.OriginalMessageId`
- `MiniBus.Exception.Type`
- `MiniBus.Exception.Message`

Immediate attempt is reset to `0` when scheduling a delayed retry. Delayed attempt increments for each scheduled retry. `MiniBus.OriginalMessageId` is set from the first received transport message id if it is not already present.

**Rationale:** Headers are already the cross-package metadata contract, and Service Bus application properties can carry these values across scheduled copies.

**Alternatives considered:**
- Store retry state only in Service Bus delivery count: rejected because immediate and delayed retries need separate counters and scheduled copies create new transport deliveries.
- Store retry state in message body: rejected because retry metadata should not mutate application contracts.

### 5. Dead-lettering includes useful retry and exception context

When all retries are exhausted, the settlement path will dead-letter with a stable reason such as `MiniBus retries exhausted` and a bounded description including exception type, exception message, immediate attempt count, delayed attempt count, and original message id when available.

**Rationale:** Operators need a dead-letter entry that explains why MiniBus stopped retrying without needing a dashboard or external metrics in this change.

**Alternatives considered:**
- Reuse the current generic dead-letter reason: rejected because it hides whether recoverability was exhausted.
- Store all diagnostics in the description: rejected because Service Bus descriptions have practical size limits, so headers should carry structured metadata too.

### 6. Preserve exception information without wrapping away the original failure

Handler and dispatch exceptions should keep their original exception object through the recoverability decision flow. Retry headers and dead-letter descriptions can copy exception type and message, but the processing path should not replace the original exception with a generic recoverability exception.

**Rationale:** Tests and callers of the no-settlement overload need original exception behavior, and future observability will need access to the original exception.

**Alternatives considered:**
- Throw a recoverability-specific wrapper for all failures: rejected because it obscures handler failures.

### 7. Keep configuration simple and explicit

The initial configuration shape will support:

```csharp
ImmediateRetries = 3
DelayedRetries = [TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)]
DeadLetterAfterRetriesExhausted = true
```

The Azure Functions DI extension should accept these options alongside existing endpoint options and document the same shape in README/sample code.

**Rationale:** This gives developers predictable defaults and a clear escape hatch without introducing exception policies or persistence concerns too early.

**Alternatives considered:**
- Add per-exception policies now: rejected as advanced exception classification is out of scope.
- Use only host.json retry settings: rejected because MiniBus needs header-preserving delayed retries and deterministic dead-letter behavior.

## Risks / Trade-offs

- **[Risk] Completing the original after scheduling a delayed retry can lose the message if scheduling succeeds but completion fails.** -> **Mitigation:** perform scheduling before completion, surface completion failures, and document that SQL outbox is required for stronger atomicity in a later change.
- **[Risk] Scheduled retry copies may accidentally get new IDs and break correlation.** -> **Mitigation:** explicitly set `MiniBus.OriginalMessageId` and preserve MiniBus correlation headers even if the Service Bus system `MessageId` must be unique.
- **[Risk] Dead-letter descriptions can exceed Service Bus limits.** -> **Mitigation:** keep descriptions bounded and store structured values in headers.
- **[Risk] Immediate retry loops could hide cancellation.** -> **Mitigation:** pass the same cancellation token through each attempt and stop retrying when cancellation is requested.

## Migration Plan

1. Add recoverability option, header, and decision types to `MiniBus.Core`.
2. Add Service Bus retry-copy creation/scheduling support to `MiniBus.AzureServiceBus`.
3. Update `MiniBusProcessor` settlement processing to execute immediate retry, delayed retry, and dead-letter decisions.
4. Update tests in core, Azure Service Bus, and Azure Functions packages.
5. Update README/sample configuration.
6. Build the solution and run all MiniBus test projects.

Rollback is straightforward before consumers adopt the new API: remove the recoverability options and scheduler types, restore the existing immediate dead-letter behavior, and remove the new tests/docs.

## Open Questions

- Should `DeadLetterAfterRetriesExhausted = false` abandon the message or propagate the exception to the Functions host after retries are exhausted?
- Should the delayed retry copy keep the Service Bus system `MessageId` equal to the original, or generate a new transport id while preserving `MiniBus.OriginalMessageId` as the stable identity?
- Should recoverability options have non-zero defaults immediately, or should sample configuration opt in explicitly while defaults remain conservative?
