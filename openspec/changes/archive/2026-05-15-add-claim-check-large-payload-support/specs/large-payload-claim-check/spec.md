## ADDED Requirements

### Requirement: Claim-check behavior is opt-in and threshold based
MiniBus SHALL provide opt-in claim-check/DataBus configuration that stores outgoing serialized payloads externally only when their serialized body length exceeds the configured threshold.

#### Scenario: Claim-check is disabled by default
- **WHEN** an application does not enable claim-check behavior
- **THEN** MiniBus sends outgoing message bodies inline using the existing serialization and dispatch behavior

#### Scenario: Below-threshold payload stays inline
- **WHEN** claim-check behavior is enabled and an outgoing serialized body length is less than or equal to the configured threshold
- **THEN** MiniBus sends the serialized body inline and does not write the payload to the payload store

#### Scenario: Above-threshold payload is claim-checked
- **WHEN** claim-check behavior is enabled and an outgoing serialized body length exceeds the configured threshold
- **THEN** MiniBus stores the serialized body through the configured payload store and sends a compact claim-check representation instead of the original body

### Requirement: Azure Blob Storage is the first claim-check provider
MiniBus SHALL support Azure Blob Storage-backed payload storage as the first claim-check provider by using MiniBus-owned payload store contracts rather than Azure SDK types in handler-facing APIs.

#### Scenario: Azure Blob payload store is configured
- **WHEN** an application enables claim-check behavior with Azure Blob payload storage
- **THEN** MiniBus uses the configured Blob-backed payload store to write and read claim-checked payload bodies

#### Scenario: Azure SDK types remain isolated
- **WHEN** handlers, message contracts, saga data, or MiniBus core contracts are compiled
- **THEN** they do not require Azure Storage SDK references to participate in claim-check message processing

### Requirement: Claim-checked wire messages carry MiniBus metadata
MiniBus SHALL send claim-checked messages with MiniBus-owned metadata that identifies the claim-check provider, payload reference, original payload content type, original payload length, and payload creation or expiry metadata when available.

#### Scenario: Claim-check metadata is added
- **WHEN** MiniBus sends an above-threshold message as a claim-check
- **THEN** the outgoing wire message contains MiniBus claim-check metadata sufficient for a receiver to validate and load the original serialized payload

#### Scenario: Message metadata is preserved
- **WHEN** MiniBus sends an above-threshold message as a claim-check
- **THEN** the outgoing wire message preserves MiniBus message type, content type, message id, correlation id, causation id, and enclosed message type metadata

#### Scenario: Compact body is sent
- **WHEN** MiniBus sends an above-threshold message as a claim-check
- **THEN** the transport body contains only a compact claim-check representation and not the original serialized payload bytes

### Requirement: Receive-side claim-check resolution restores original payloads
MiniBus SHALL resolve claim-checked payload references before message deserialization so handlers and sagas receive the original message contract.

#### Scenario: Claim-checked body is resolved before deserialization
- **WHEN** MiniBus receives a message containing valid claim-check metadata
- **THEN** MiniBus loads the referenced payload bytes and deserializes those bytes as the received MiniBus message type

#### Scenario: Handler receives original contract
- **WHEN** a claim-checked message is processed successfully
- **THEN** the matching handler or saga receives the original message contract instance and does not receive the claim-check representation

#### Scenario: Inline body bypasses claim-check resolution
- **WHEN** MiniBus receives a message without claim-check metadata
- **THEN** MiniBus deserializes the received body using the existing inline message behavior

### Requirement: Claim-check failures flow through processing failure handling
MiniBus SHALL report invalid or missing claim-check references as processing failures that can be handled by existing recoverability behavior.

#### Scenario: Missing payload fails clearly
- **WHEN** MiniBus receives a claim-checked message whose referenced payload cannot be found
- **THEN** MiniBus raises a clear claim-check payload-not-found failure before handler invocation

#### Scenario: Invalid claim-check reference fails clearly
- **WHEN** MiniBus receives claim-check metadata that is malformed, unsupported, or incompatible with the configured provider
- **THEN** MiniBus raises a clear invalid claim-check reference failure before deserialization

#### Scenario: Payload store is not configured
- **WHEN** MiniBus receives a claim-checked message but no matching payload store provider is configured
- **THEN** MiniBus raises a clear claim-check configuration failure before deserialization

### Requirement: Claim-check behavior is documented and tested
MiniBus SHALL document and test claim-check configuration, threshold behavior, direct dispatch, scheduled dispatch, receive-side resolution, SQL outbox replay, and failure handling.

#### Scenario: Documentation shows setup
- **WHEN** a developer reads MiniBus claim-check documentation or samples
- **THEN** it shows how to enable threshold-based claim-check behavior with Azure Blob payload storage and explains payload retention requirements

#### Scenario: Unit tests cover claim-check decisions
- **WHEN** the normal test suite runs
- **THEN** it verifies below-threshold inline behavior, above-threshold claim-check behavior, metadata creation, and invalid reference handling without requiring live Azure resources

#### Scenario: Integration-style tests cover Blob-backed round trips
- **WHEN** Azure Storage-backed tests run with available storage infrastructure
- **THEN** they verify that an above-threshold payload can be stored, sent as a claim-check, resolved, and deserialized back to the original message contract
