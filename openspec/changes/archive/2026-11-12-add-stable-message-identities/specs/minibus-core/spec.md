## ADDED Requirements

### Requirement: Core message identity services remain transport independent
MiniBus Core SHALL own stable message identity registration, outgoing identity selection, and incoming identity resolution without depending on Azure Functions, Azure Service Bus, SQL, or Azure Storage types.

#### Scenario: Core registry is used outside Azure hosting
- **WHEN** an application or transport component uses the message identity registry
- **THEN** it can register and resolve MiniBus message contract identities using only `MiniBus.Core` types

#### Scenario: Serializer receives a resolved CLR type
- **WHEN** MiniBus resolves an incoming stable or legacy identity
- **THEN** the existing `IMessageSerializer` deserializes the body using the resolved concrete CLR `Type`
