## 1. Core recoverability model

- [x] 1.1 Add `MiniBus.Core` recoverability options for immediate retries, delayed retry delays, and dead-letter-after-exhaustion behavior.
- [x] 1.2 Add core retry header constants for immediate attempt, delayed attempt, retry limits, original message id, exception type, and exception message.
- [x] 1.3 Add a transport-independent recoverability decision model for immediate retry, delayed retry, dead-letter, and propagate/no-settlement outcomes.
- [x] 1.4 Add a decision component that evaluates retry headers, configured options, and the current exception without referencing Azure SDK or Azure Functions types.
- [x] 1.5 Add core unit tests for decision outcomes, attempt counting, retry exhaustion, and header updates.

## 2. Azure Service Bus delayed retry scheduling

- [x] 2.1 Add a Service Bus retry scheduler abstraction/implementation in `MiniBus.AzureServiceBus` that can schedule a retry copy of a `ServiceBusReceivedMessage`.
- [x] 2.2 Create scheduled retry messages by preserving the original body, message type metadata, correlation headers, content type, and supported application properties.
- [x] 2.3 Set or preserve `MiniBus.OriginalMessageId`, update delayed retry headers, reset immediate retry headers, and include exception headers on scheduled retry copies.
- [x] 2.4 Ensure delayed retry scheduling calls the existing Service Bus sender scheduling path and does not invoke handler outgoing dispatch APIs.
- [x] 2.5 Add Azure Service Bus unit tests for retry-copy headers, original message id/correlation preservation, due-time selection, and scheduled sender calls.

## 3. Azure Functions recoverability execution

- [x] 3.1 Register recoverability options and collaborators through the Azure Functions DI extension.
- [x] 3.2 Update settlement-enabled `MiniBusProcessor` processing to retry handler invocation immediately inside the same invocation while attempts remain.
- [x] 3.3 Ensure immediate retries do not create Service Bus messages and do not complete or dead-letter until a final decision is reached.
- [x] 3.4 When immediate retries are exhausted and a delayed retry remains, schedule a delayed retry copy and then complete the original received message.
- [x] 3.5 When all configured retries are exhausted, dead-letter the received message only if `DeadLetterAfterRetriesExhausted` is enabled.
- [x] 3.6 Preserve the current no-settlement overload behavior by propagating processing failures with original exception information.
- [x] 3.7 Ensure cancellation stops retry loops and propagates rather than being converted to dead-letter retry behavior.

## 4. Dead-letter diagnostics

- [x] 4.1 Use a stable dead-letter reason for retries exhausted.
- [x] 4.2 Build a bounded dead-letter description containing exception type, exception message, immediate attempt, delayed attempt, retry limits, and original message id when available.
- [x] 4.3 Add/update retry and exception headers before delayed retry scheduling and final dead-letter settlement.
- [x] 4.4 Add tests verifying useful dead-letter reason and description values after retries are exhausted.

## 5. Tests and documentation

- [x] 5.1 Add Azure Functions tests proving a failing handler is retried immediately and then succeeds without creating scheduled messages.
- [x] 5.2 Add Azure Functions tests proving immediate exhaustion schedules the configured delayed retry and completes the original message.
- [x] 5.3 Add Azure Functions tests proving all retries exhausted dead-letters the original message and does not complete it.
- [x] 5.4 Add tests proving correlation headers, original message id, and retry headers flow through immediate and delayed retry paths.
- [x] 5.5 Update `src/MiniBus.AzureFunctions/README.md` and sample configuration to show recoverability options:

  ```csharp
  ImmediateRetries = 3
  DelayedRetries =
  [
      TimeSpan.FromSeconds(10),
      TimeSpan.FromMinutes(1),
      TimeSpan.FromMinutes(5)
  ],
  DeadLetterAfterRetriesExhausted = true
  ```

- [x] 5.6 Build the solution and run `MiniBus.Core.Tests`, `MiniBus.AzureServiceBus.Tests`, and `MiniBus.AzureFunctions.Tests`.
