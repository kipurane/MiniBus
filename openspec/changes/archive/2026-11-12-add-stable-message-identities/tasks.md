## 1. Core Identity Registry

- [x] 1.1 Add the transport-independent stable message identity registry and public registration, outgoing selection, and incoming resolution APIs to `MiniBus.Core`.
- [x] 1.2 Implement ordinal case-sensitive validation for blank identities, non-message contract types, duplicate aliases, cross-type conflicts, and multiple primary identities.
- [x] 1.3 Implement assembly-qualified outgoing and `Type.GetType` incoming compatibility fallbacks for unregistered identities, while rejecting fallback types that are not MiniBus message contracts.
- [x] 1.4 Add core unit tests for primary identities, receive-only aliases, CLR rename aliases, case sensitivity, conflicts, unknown identities, and both compatibility fallbacks.

## 2. Shared Configuration And Outgoing Metadata

- [x] 2.1 Expose identity registry configuration through the MiniBus Azure Functions setup path and register the configured registry as the shared singleton used by composed MiniBus services.
- [x] 2.2 Update MiniBus outgoing header creation and claim-check transformation to write the registered primary identity to `MiniBus.MessageType` and `MiniBus.EnclosedMessageTypes` while preserving fallback behavior.
- [x] 2.3 Thread the registry through Azure Service Bus message factory and dispatcher construction without breaking existing transport-only construction paths.
- [x] 2.4 Add transport tests proving direct send, publish, and schedule use stable identities, aliases are never emitted, and unregistered contracts retain CLR identities.

## 3. Receive Processing And Retry Compatibility

- [x] 3.1 Update Azure Functions message type resolution to check registered primary identities and aliases before the CLR fallback and to pass the resolved concrete type to the existing serializer.
- [x] 3.2 Preserve the received identity unchanged when creating delayed retry copies and ensure normal correlation, causation, and retry headers remain intact.
- [x] 3.3 Add Azure Functions tests for stable identity processing, alias processing after a simulated CLR rename, legacy CLR identity processing, unknown identity recoverability, and incompatible-body deserialization failure.
- [x] 3.4 Extend live or emulator-backed transport coverage where appropriate to verify stable identity application properties survive broker round trips and delayed retry scheduling.

## 4. SQL Outbox Compatibility

- [x] 4.1 Update SQL outbox serialization to store the selected stable identity in the `MessageType` column and message headers without changing the SQL schema.
- [x] 4.2 Update SQL outbox deserialization to resolve stored primary identities, aliases, and legacy CLR identities through the shared registry.
- [x] 4.3 Ensure outbox replay preserves the stored message type header instead of recomputing a different identity from the resolved CLR type used for routing.
- [x] 4.4 Add SQL unit and integration tests for new stable rows, existing CLR-name rows, renamed-contract alias rows, claim-checked stable rows, replay metadata preservation, and unknown-identity failure retry metadata.

## 5. Claim-Check Identity Consistency

- [x] 5.1 Ensure inline and above-threshold claim-check paths select identical stable message identity metadata from the shared registry.
- [x] 5.2 Ensure receive-side claim-check resolution resolves stable identities and aliases before deserializing the externally stored body.
- [x] 5.3 Add claim-check unit and Blob-backed round-trip tests for stable primary identities, aliases, inline/large-payload consistency, and unknown identities.

## 6. Analyzer Guidance

- [x] 6.1 Add stable analyzer diagnostic descriptors for invalid and conflicting visible message identity registrations.
- [x] 6.2 Implement high-confidence analysis for blank literal values, one literal identity mapped to different contracts, and one contract assigned different visible primary identities while ignoring dynamic or incomplete configuration.
- [x] 6.3 Add Roslyn analyzer tests for every reported mistake, valid registrations, case-sensitive distinct identities, aliases, and dynamic-registration false-positive boundaries.
- [x] 6.4 Document the new diagnostic IDs with invalid examples, valid fixes, severity, and suppression guidance.

## 7. Samples, Documentation, And Verification

- [x] 7.1 Configure stable message identities in the project template and Billing/Inventory reference applications using consistent shared values across endpoints.
- [x] 7.2 Document stable identity naming, identity immutability, receive aliases, producer/consumer rollout order, rollback constraints, and the distinction from payload versioning or upcasting.
- [x] 7.3 Update root, Core, Azure Service Bus, Azure Functions, SQL persistence, claim-check, analyzer, and template documentation for the new configuration and compatibility behavior.
- [x] 7.4 Run focused Core, Azure Service Bus, Azure Functions, SQL persistence, Azure Storage, analyzer, template, and acceptance tests, then run the full solution build and test suite.
