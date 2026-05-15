## ADDED Requirements

### Requirement: Delayed retries preserve claim-check metadata
MiniBus recoverability SHALL preserve MiniBus claim-check metadata when scheduling delayed retry copies of claim-checked messages.

#### Scenario: Delayed retry copy keeps claim-check metadata
- **WHEN** a claim-checked message fails and MiniBus schedules a delayed retry
- **THEN** the scheduled retry message contains the compact claim-check body and the MiniBus claim-check headers needed to resolve the original payload later

#### Scenario: Retry metadata is added without removing claim-check metadata
- **WHEN** MiniBus creates a delayed retry copy for a claim-checked message
- **THEN** it increments retry headers and preserves existing claim-check provider, payload reference, content type, and payload length metadata

### Requirement: Claim-check resolution failures use existing recoverability decisions
MiniBus recoverability SHALL handle claim-check resolution failures through the same immediate retry, delayed retry, dead-letter, and propagation decisions used for handler failures.

#### Scenario: Missing payload can be retried
- **WHEN** claim-check resolution fails because the referenced payload is unavailable and retry policy allows another retry
- **THEN** MiniBus applies the next configured immediate or delayed retry decision

#### Scenario: Missing payload can be dead-lettered after retries
- **WHEN** claim-check resolution continues to fail after all configured retries are exhausted
- **THEN** MiniBus dead-letters or propagates according to the configured exhausted-retry behavior

#### Scenario: Failure diagnostics identify claim-check resolution
- **WHEN** claim-check resolution fails
- **THEN** the recoverability failure path includes exception information that identifies the failure as a claim-check payload or reference problem
