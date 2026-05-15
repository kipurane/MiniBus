## Why

MiniBus now has durable saga persistence, SQL inbox/outbox capture, and Azure Service Bus scheduled dispatch, but sagas still lack a first-class way to request and handle future timeout messages. This change closes the remaining saga workflow gap by making timeouts explicit while reusing the existing Service Bus scheduled-message and outbox paths.

## What Changes

- Add saga timeout APIs or conventions so saga handlers can request a timeout message for a future due time.
- Model timeout messages as normal MiniBus messages that are scheduled through Azure Service Bus and later processed by the existing saga invocation pipeline.
- Require timeout messages to correlate to existing saga state unless the saga explicitly configures a timeout message as a starting message.
- Preserve MiniBus correlation, causation, and message metadata when a saga requests a timeout.
- Ensure SQL outbox-enabled processing stores requested timeout schedules atomically with saga state changes and inbox completion.
- Keep this change scoped to Service Bus scheduled messages; do not add a SQL timeout table or SQL-managed timeout dispatcher.

## Capabilities

### New Capabilities

- `saga-timeouts`: Defines saga timeout request, scheduling, correlation, dispatch, persistence, and verification behavior using Service Bus scheduled messages.

### Modified Capabilities

- None.

## Impact

- Core saga contracts and invocation APIs may gain timeout request helpers or conventions.
- Azure Functions processing will carry saga timeout requests through the existing `MiniBusContext.Schedule(...)` behavior.
- SQL inbox/outbox persistence will continue to store scheduled operations with due times and must be verified for saga timeout scenarios.
- Azure Service Bus transport routing and scheduled dispatch will be used for timeout messages without introducing new Azure SDK dependencies into saga handlers.
- Tests will cover core saga timeout request behavior, correlation behavior, SQL outbox capture, direct scheduled dispatch, and documented sample usage.
