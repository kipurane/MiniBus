## ADDED Requirements

### Requirement: Claim-check processing preserves stable message identities
MiniBus claim-check behavior SHALL select, store, transmit, and resolve message identities through the same configured registry used for inline messages.

#### Scenario: Registered large message is claim-checked
- **WHEN** a contract with a registered stable identity exceeds the claim-check threshold
- **THEN** its compact wire message and claim-check metadata preserve the registered stable identity

#### Scenario: Claim-checked stable identity is received
- **WHEN** MiniBus receives valid claim-check metadata with a registered stable message identity
- **THEN** it resolves the external body and deserializes it using the CLR contract mapped by that identity

#### Scenario: Claim-checked alias is received
- **WHEN** MiniBus receives a claim-checked message whose identity is a registered alias
- **THEN** it resolves the payload and processes it using the current CLR contract type

#### Scenario: Inline and claim-checked paths are compared
- **WHEN** the same registered contract is sent below and above the claim-check threshold
- **THEN** both wire messages use the same stable message identity
