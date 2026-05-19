## ADDED Requirements

### Requirement: Analyzer package is distributed separately
MiniBus SHALL provide a dedicated Roslyn analyzer package for compile-time MiniBus diagnostics without adding Roslyn dependencies to MiniBus runtime packages.

#### Scenario: Application references analyzer package
- **WHEN** a consuming application references the MiniBus analyzer package as an analyzer-only package reference
- **THEN** MiniBus analyzer diagnostics participate in normal C# build analysis

#### Scenario: Runtime package is packed
- **WHEN** MiniBus runtime packages are packed
- **THEN** they do not include Roslyn analyzer implementation dependencies as runtime dependencies

### Requirement: Analyzer diagnostics use stable MiniBus identifiers
MiniBus analyzers SHALL report diagnostics with stable MiniBus analyzer IDs, titles, categories, default severities, and messages that identify the invalid MiniBus usage.

#### Scenario: Analyzer reports a diagnostic
- **WHEN** a MiniBus analyzer detects invalid or suspicious MiniBus usage
- **THEN** the diagnostic includes a stable MiniBus analyzer diagnostic ID that does not conflict with source-generator diagnostic IDs

#### Scenario: Diagnostic is documented
- **WHEN** a developer reads the analyzer package documentation
- **THEN** the documentation describes each analyzer diagnostic, its severity, an invalid example, a valid example, and suppression guidance

### Requirement: Handler analyzers detect invalid handler types
MiniBus analyzers SHALL detect high-confidence mistakes in types that implement `IHandleMessages<TMessage>`.

#### Scenario: Abstract handler implements MiniBus handler contract
- **WHEN** a handler type implements `IHandleMessages<TMessage>` but cannot be instantiated because it is abstract
- **THEN** the analyzer reports a handler diagnostic for the invalid handler type

#### Scenario: Open generic handler implements MiniBus handler contract
- **WHEN** a handler type implements `IHandleMessages<TMessage>` but remains an open generic type in a MiniBus handler registration or discovery context
- **THEN** the analyzer reports a handler diagnostic for the invalid handler type

#### Scenario: Valid concrete handler implements MiniBus handler contract
- **WHEN** a concrete handler implements `IHandleMessages<TMessage>` for a valid MiniBus message contract with the expected `Handle` method signature
- **THEN** the analyzer does not report a handler-shape diagnostic

### Requirement: Message contract analyzers detect confusing marker usage
MiniBus analyzers SHALL detect high-confidence mistakes in MiniBus message contract marker usage.

#### Scenario: Type is used where command is required
- **WHEN** a type is supplied to a visible MiniBus command API that requires `ICommand` and the type does not implement `ICommand`
- **THEN** the analyzer reports a message contract diagnostic when the compiler does not already provide an equivalent error at that location

#### Scenario: Message type implements both command and event markers
- **WHEN** a message type implements both `ICommand` and `IEvent`
- **THEN** the analyzer reports a message contract diagnostic describing the ambiguous MiniBus message role

#### Scenario: Valid command contract is used
- **WHEN** a message type implements `ICommand` and is used with command-specific MiniBus APIs
- **THEN** the analyzer does not report a message contract diagnostic

### Requirement: Routing analyzers detect visible routing mistakes
MiniBus analyzers SHALL detect visible, high-confidence mistakes in Azure Service Bus route configuration and route-dependent MiniBus usage.

#### Scenario: Route destination is constant whitespace
- **WHEN** route configuration calls `MapCommand`, `MapEvent`, or `MapScheduledMessage` with a compile-time constant empty or whitespace destination
- **THEN** the analyzer reports a routing diagnostic for the invalid destination

#### Scenario: Command send has no visible route
- **WHEN** a compilation contains a direct send of a command type through MiniBus context and visible route configuration for that application does not include that command type
- **THEN** the analyzer reports a routing diagnostic only when the missing route can be determined without whole-program assumptions

#### Scenario: Route configuration is dynamic
- **WHEN** route configuration or outgoing message usage depends on values or assemblies not visible to the analyzer
- **THEN** the analyzer does not report a route completeness diagnostic based only on incomplete information

### Requirement: Azure Functions analyzers detect visible setup mistakes
MiniBus analyzers SHALL detect visible Azure Functions setup mistakes that would prevent MiniBus processing from being registered correctly.

#### Scenario: MiniBus processor is used without visible registration
- **WHEN** an application contains visible Azure Functions MiniBus processor usage but no visible `AddMiniBusAzureFunctions` registration in the analyzed startup or configuration code
- **THEN** the analyzer reports a configuration diagnostic only when the missing registration can be determined without whole-program assumptions

#### Scenario: Source-generated wrapper diagnostics already exist
- **WHEN** a source-generated wrapper declaration has a problem already reported by `MiniBus.AzureFunctions.SourceGenerators`
- **THEN** the MiniBus analyzer package does not report a duplicate diagnostic for the same wrapper declaration problem

#### Scenario: Valid Azure Functions setup is visible
- **WHEN** an application visibly registers `AddMiniBusAzureFunctions` and uses manual or generated wrappers that delegate to MiniBus processing
- **THEN** the analyzer does not report an Azure Functions setup diagnostic

### Requirement: Saga analyzers are limited to reliable evidence
MiniBus analyzers SHALL include saga diagnostics only when saga usage and configuration are visible enough to avoid speculative results.

#### Scenario: Saga usage is visible while sagas are visibly disabled
- **WHEN** saga handling or timeout usage is visible in the analyzed compilation and MiniBus Azure Functions options visibly leave saga processing disabled
- **THEN** the analyzer reports a saga configuration diagnostic

#### Scenario: Saga configuration is not statically knowable
- **WHEN** saga registration or `EnableSagas` configuration is split across dynamic or external code that is not visible to the analyzer
- **THEN** the analyzer does not report a saga configuration diagnostic based only on incomplete information

### Requirement: Analyzer behavior is covered by Roslyn tests
MiniBus SHALL verify analyzer diagnostics and false-positive behavior with automated Roslyn analyzer tests.

#### Scenario: Invalid examples are analyzed
- **WHEN** analyzer tests compile invalid MiniBus usage examples
- **THEN** the tests verify the expected diagnostic IDs, severities, and locations

#### Scenario: Valid examples are analyzed
- **WHEN** analyzer tests compile valid MiniBus usage examples
- **THEN** the tests verify that MiniBus analyzers do not report diagnostics

### Requirement: Analyzer package documentation explains usage and limits
MiniBus SHALL document analyzer installation, diagnostics, examples, suppression guidance, and static-analysis limitations.

#### Scenario: Developer installs analyzers
- **WHEN** a developer reads the analyzer package README
- **THEN** it shows the package reference shape for analyzer-only consumption

#### Scenario: Developer evaluates analyzer output
- **WHEN** a developer reads a reported MiniBus analyzer diagnostic
- **THEN** package documentation explains the rule, why it matters, and how to fix or suppress it intentionally
