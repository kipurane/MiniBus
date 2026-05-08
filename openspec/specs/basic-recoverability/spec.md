# basic-recoverability Specification

## Purpose
Defines basic recoverability for MiniBus message processing in Azure Functions with Azure Service Bus, including immediate retries, delayed retries through scheduled Service Bus message copies, retry headers, and retries-exhausted dead-letter behavior.

## Requirements
### Requirement: Recoverability options configure retry behavior
MiniBus SHALL provide configuration for immediate retry count, delayed retry intervals, and whether messages are dead-lettered after all configured retries are exhausted.

#### Scenario: Application configures basic recoverability
- **WHEN** an application configures MiniBus recoverability with `ImmediateRetries = 3`, delayed retries of 10 seconds, 1 minute, and 5 minutes, and `DeadLetterAfterRetriesExhausted = true`
- **THEN** MiniBus uses those values when deciding how to handle processing failures

#### Scenario: Retry limits are exposed as headers
- **WHEN** MiniBus processes a message under a configured recoverability policy
- **THEN** retry metadata includes `MiniBus.Retry.MaxImmediateAttempts` and `MiniBus.Retry.MaxDelayedAttempts`

### Requirement: Recoverability decision model is transport independent
MiniBus.Core SHALL define a recoverability decision model that represents immediate retry, delayed retry, dead-letter, and propagation outcomes without referencing Azure Service Bus or Azure Functions APIs.

#### Scenario: Immediate retry decision is produced
- **WHEN** processing fails and the current immediate attempt is below the configured immediate retry limit
- **THEN** the decision model returns an immediate retry decision

#### Scenario: Delayed retry decision is produced
- **WHEN** processing fails, immediate retries are exhausted, and a configured delayed retry remains
- **THEN** the decision model returns a delayed retry decision with the next delay

#### Scenario: Dead-letter decision is produced
- **WHEN** processing fails and both immediate and delayed retries are exhausted with dead-lettering enabled
- **THEN** the decision model returns a dead-letter decision

### Requirement: Retry metadata is stored in MiniBus headers
MiniBus SHALL store retry state and failure metadata in MiniBus headers rather than message bodies or transport-only delivery counters.

#### Scenario: Retry headers are written after failure
- **WHEN** a processing failure is handled by recoverability
- **THEN** headers include immediate attempt, delayed attempt, maximum immediate attempts, maximum delayed attempts, exception type, and exception message values

#### Scenario: Original message id is recorded
- **WHEN** a message enters recoverability and `MiniBus.OriginalMessageId` is missing
- **THEN** MiniBus sets `MiniBus.OriginalMessageId` from the first received message id

### Requirement: Immediate retries occur within the same processing invocation
The Azure Functions adapter SHALL execute immediate retries inside the same `MiniBusProcessor` invocation and MUST NOT create new Azure Service Bus messages for immediate retries.

#### Scenario: Handler succeeds after immediate retry
- **WHEN** a handler fails on the first attempt and succeeds on a configured immediate retry
- **THEN** MiniBus invokes the handler again in the same processor call and completes the original Service Bus message

#### Scenario: Immediate retry does not schedule messages
- **WHEN** a failure is handled by an immediate retry decision
- **THEN** MiniBus does not call Azure Service Bus scheduled message APIs

### Requirement: Delayed retries use scheduled Service Bus message copies
MiniBus SHALL implement delayed retries for Azure Service Bus by scheduling a copy of the original received Service Bus message for the configured future enqueue time.

#### Scenario: Delayed retry is scheduled after immediate retries are exhausted
- **WHEN** immediate retries are exhausted and the next delayed retry interval is available
- **THEN** MiniBus schedules one Service Bus message copy for that interval and completes the original received message

#### Scenario: Delayed retry copy preserves body and message type metadata
- **WHEN** MiniBus creates a delayed retry copy
- **THEN** the scheduled message contains the original body and MiniBus message type headers

#### Scenario: Delayed retry copy updates retry counters
- **WHEN** MiniBus schedules a delayed retry
- **THEN** the scheduled copy increments `MiniBus.Retry.DelayedAttempt` and resets `MiniBus.Retry.ImmediateAttempt`

### Requirement: Correlation and original message identity are preserved across retries
MiniBus SHALL preserve correlation metadata and original message identity through immediate and delayed retry flows.

#### Scenario: Correlation headers survive delayed retry scheduling
- **WHEN** a failed message with correlation headers is scheduled for delayed retry
- **THEN** the scheduled retry message contains the same MiniBus correlation headers

#### Scenario: Original message id survives retry copies
- **WHEN** a delayed retry message is scheduled from an original Service Bus message
- **THEN** `MiniBus.OriginalMessageId` on the scheduled message identifies the first received message id

### Requirement: Dead-letter occurs only after retries are exhausted
The Azure Functions adapter SHALL dead-letter a failed Service Bus message only after the configured immediate and delayed retry policy is exhausted.

#### Scenario: Message is not dead-lettered while immediate retries remain
- **WHEN** a handler fails and an immediate retry remains
- **THEN** MiniBus does not dead-letter the received message

#### Scenario: Message is not dead-lettered while delayed retries remain
- **WHEN** a handler fails after immediate retries are exhausted and a delayed retry remains
- **THEN** MiniBus schedules the delayed retry and does not dead-letter the received message

#### Scenario: Message is dead-lettered after all retries are exhausted
- **WHEN** a handler fails and no immediate or delayed retries remain
- **THEN** MiniBus dead-letters the received Service Bus message

### Requirement: Dead-letter diagnostics describe retry exhaustion
MiniBus SHALL use a useful dead-letter reason and bounded description when retries are exhausted.

#### Scenario: Dead-letter reason identifies retry exhaustion
- **WHEN** MiniBus dead-letters a message because all retries are exhausted
- **THEN** the dead-letter reason indicates MiniBus retries were exhausted

#### Scenario: Dead-letter description includes failure context
- **WHEN** MiniBus dead-letters a message because retries are exhausted
- **THEN** the dead-letter description includes the exception type, exception message, immediate attempt, delayed attempt, retry limits, and original message id when available

### Requirement: Original exception information is preserved
MiniBus SHALL preserve the original processing exception through recoverability decisions and no-settlement processing.

#### Scenario: No-settlement processing propagates original handler exception
- **WHEN** the no-settlement processor overload handles a message whose handler throws
- **THEN** the original handler exception is propagated to the caller

#### Scenario: Settlement processing records original exception metadata
- **WHEN** settlement-enabled processing handles a failure through delayed retry or dead-letter
- **THEN** retry headers and dead-letter diagnostics use the original exception type and message

### Requirement: Recoverability behavior is documented and tested
MiniBus SHALL document the basic recoverability configuration shape and cover immediate retry, delayed retry, and retries-exhausted behavior with unit tests.

#### Scenario: Documentation shows sample recoverability configuration
- **WHEN** a developer reads the Azure Functions MiniBus documentation or sample configuration
- **THEN** it shows immediate retries, delayed retry intervals, and dead-letter-after-exhaustion options

#### Scenario: Unit tests cover recoverability outcomes
- **WHEN** the test suite runs
- **THEN** it verifies immediate retry success, delayed retry scheduling, retries-exhausted dead-lettering, and retry/correlation header preservation
