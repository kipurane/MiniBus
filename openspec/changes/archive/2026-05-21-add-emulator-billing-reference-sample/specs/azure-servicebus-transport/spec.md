## ADDED Requirements

### Requirement: Azure Service Bus sample shows emulator dispatch path
The Azure Service Bus transport documentation and samples SHALL show the real local transport-dispatch path used by the emulator-backed Billing reference workflow.

#### Scenario: Emulator-backed sample configures sender services
- **WHEN** a developer inspects the emulator-backed Billing sample registration
- **THEN** it shows connection-backed Azure Service Bus sender and client registration for MiniBus outgoing dispatch rather than relying on a throwing placeholder sender

#### Scenario: Local routes match emulator topology
- **WHEN** a developer runs the emulator-backed Billing workflow
- **THEN** the configured command, event, and scheduled-message routes target destinations declared by the local emulator topology
