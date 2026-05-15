## ADDED Requirements

### Requirement: Azure Functions processing resolves claim-checks before deserialization
The Azure Functions adapter SHALL resolve MiniBus claim-check payload references before deserializing the received message body.

#### Scenario: Claim-check resolution precedes deserialization
- **WHEN** `MiniBusProcessor` receives a Service Bus message with valid MiniBus claim-check metadata
- **THEN** it loads the referenced payload body before invoking the configured MiniBus message serializer

#### Scenario: Message type resolution is preserved
- **WHEN** `MiniBusProcessor` resolves a claim-checked message
- **THEN** it uses the MiniBus message type headers from the received Service Bus message to deserialize the restored payload body

#### Scenario: Handler metadata is preserved
- **WHEN** a handler processes a resolved claim-checked message
- **THEN** its `MiniBusContext` exposes the original MiniBus headers including message id, correlation id, causation id, content type, and claim-check metadata

### Requirement: Azure Functions processing handles claim-check resolution failures through recoverability
The Azure Functions adapter SHALL treat claim-check resolution failures as processing failures before handler or saga invocation.

#### Scenario: Missing claim-check payload enters recoverability
- **WHEN** `MiniBusProcessor` cannot find a referenced claim-check payload
- **THEN** processing fails before deserialization and existing recoverability behavior decides whether to retry, delay, dead-letter, or propagate

#### Scenario: Invalid claim-check metadata enters recoverability
- **WHEN** `MiniBusProcessor` receives malformed or unsupported claim-check metadata
- **THEN** processing fails before deserialization and existing recoverability behavior decides whether to retry, delay, dead-letter, or propagate

#### Scenario: Inline messages continue through existing path
- **WHEN** `MiniBusProcessor` receives a Service Bus message without MiniBus claim-check metadata
- **THEN** it deserializes the received body using the existing inline processing path
