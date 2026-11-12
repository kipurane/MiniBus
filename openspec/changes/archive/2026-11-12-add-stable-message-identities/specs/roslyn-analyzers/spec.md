## ADDED Requirements

### Requirement: Analyzer detects visible stable identity registration mistakes
MiniBus analyzers SHALL report high-confidence diagnostics for invalid or conflicting stable message identity registrations when the relevant values and registrations are statically visible.

#### Scenario: Literal identity is empty or whitespace
- **WHEN** a stable identity registration uses a compile-time constant empty or whitespace-only primary identity or alias
- **THEN** the analyzer reports a stable identity configuration diagnostic

#### Scenario: Literal identity maps to different contracts
- **WHEN** visible stable identity registrations map the same compile-time constant identity to different MiniBus contract types
- **THEN** the analyzer reports a conflicting identity diagnostic

#### Scenario: Contract has different visible primary identities
- **WHEN** visible registrations assign different compile-time constant primary identities to the same contract type
- **THEN** the analyzer reports a conflicting identity diagnostic

#### Scenario: Identity registration is dynamic
- **WHEN** an identity value or relevant registration is not statically knowable in the analyzed compilation
- **THEN** the analyzer does not report a completeness or conflict diagnostic based on incomplete information

#### Scenario: Valid identity registration is visible
- **WHEN** visible MiniBus contract registrations use non-empty, non-conflicting primary identities and aliases
- **THEN** the analyzer does not report a stable identity diagnostic
