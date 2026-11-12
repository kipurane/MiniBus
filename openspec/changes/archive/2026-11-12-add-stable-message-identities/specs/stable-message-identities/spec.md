## ADDED Requirements

### Requirement: Applications register stable message identities
MiniBus SHALL provide a transport-independent registry that maps a MiniBus message contract type to one primary stable wire identity and zero or more receive aliases.

#### Scenario: Primary identity is registered
- **WHEN** an application registers a non-empty stable identity for an `IMessage` contract type
- **THEN** the registry maps that CLR type to the primary outgoing identity and maps the identity back to that CLR type

#### Scenario: Receive aliases are registered
- **WHEN** an application registers legacy aliases for a message contract type
- **THEN** each alias resolves to that CLR type but does not replace the primary outgoing identity

#### Scenario: Non-message type is registered
- **WHEN** an application attempts to register a stable identity for a type that does not implement `IMessage`
- **THEN** MiniBus rejects the registration with a clear configuration error

### Requirement: Stable identity registration is deterministic
MiniBus SHALL compare message identities using ordinal case-sensitive semantics and MUST reject invalid or ambiguous mappings during registration.

#### Scenario: Identity is empty or whitespace
- **WHEN** an application registers a null, empty, or whitespace-only primary identity or alias
- **THEN** MiniBus rejects the registration with a clear argument or configuration error

#### Scenario: Identity maps to different contract types
- **WHEN** the same primary identity or alias is registered for more than one CLR message contract type
- **THEN** MiniBus rejects the conflicting registration and identifies the identity and contract types

#### Scenario: Contract receives different primary identities
- **WHEN** an application attempts to register a second different primary identity for the same CLR message contract type
- **THEN** MiniBus rejects the conflicting registration instead of changing outgoing behavior based on registration order

#### Scenario: Identity differs only by casing
- **WHEN** two registered identities differ only by letter casing
- **THEN** MiniBus treats them as distinct identity values

### Requirement: Outgoing identity selection is backward compatible
MiniBus SHALL write the registered primary stable identity for a registered message contract and SHALL retain the existing assembly-qualified CLR identity fallback for an unregistered contract.

#### Scenario: Registered contract is sent
- **WHEN** MiniBus creates outgoing metadata for a contract with a registered primary identity
- **THEN** `MiniBus.MessageType` and `MiniBus.EnclosedMessageTypes` contain the registered primary identity

#### Scenario: Unregistered contract is sent
- **WHEN** MiniBus creates outgoing metadata for a contract without a registered primary identity
- **THEN** MiniBus uses the existing assembly-qualified CLR type identity

#### Scenario: Contract has aliases
- **WHEN** MiniBus creates outgoing metadata for a contract with a primary identity and one or more aliases
- **THEN** MiniBus writes only the primary identity and does not select an alias for outgoing metadata

### Requirement: Incoming identity resolution supports stable and legacy values
MiniBus SHALL resolve incoming primary identities and aliases through the registry before using the existing CLR type resolution fallback.

#### Scenario: Primary stable identity is received
- **WHEN** an incoming message contains a registered primary identity
- **THEN** MiniBus resolves the identity to the registered CLR message contract type

#### Scenario: Registered alias is received after a CLR rename
- **WHEN** an incoming message contains an alias registered for the current CLR contract type
- **THEN** MiniBus resolves the alias to the current CLR contract type without requiring the historical CLR type to exist

#### Scenario: Legacy assembly-qualified identity is received
- **WHEN** an incoming message contains an unregistered assembly-qualified identity that resolves to a current MiniBus message contract type
- **THEN** MiniBus accepts the identity through the compatibility fallback

#### Scenario: Unknown identity is received
- **WHEN** an incoming message identity matches no primary identity or alias and cannot be resolved through the CLR fallback
- **THEN** MiniBus reports a clear message identity resolution failure before deserialization or handler invocation

### Requirement: Identity resolution remains separate from body deserialization
MiniBus SHALL resolve a wire identity to a concrete CLR `Type` before invoking the configured `IMessageSerializer` and SHALL NOT treat stable identity resolution as payload upcasting.

#### Scenario: Stable identity resolves successfully
- **WHEN** MiniBus resolves a stable identity to a CLR message contract type
- **THEN** it passes the received body and resolved concrete type to the configured serializer

#### Scenario: Historical payload is incompatible
- **WHEN** a stable identity resolves but the received body is incompatible with the current CLR contract
- **THEN** the existing deserialization failure path applies without an implicit payload transformation
