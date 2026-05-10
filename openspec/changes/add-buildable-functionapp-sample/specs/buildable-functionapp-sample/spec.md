## ADDED Requirements

### Requirement: Function App sample is buildable
MiniBus SHALL provide a buildable Azure Functions isolated worker sample project under `samples/MiniBus.Samples.FunctionApp`.

#### Scenario: Sample project builds with the solution
- **WHEN** the solution is built or tested
- **THEN** the sample project compiles against the current MiniBus project references

#### Scenario: Sample project is included in the solution
- **WHEN** a developer opens `MiniBus.sln`
- **THEN** the Function App sample appears as a project rather than only loose solution items

### Requirement: Sample demonstrates host registration
The Function App sample SHALL show a minimal host startup path for registering MiniBus and Azure Functions isolated worker services.

#### Scenario: Developer inspects sample startup
- **WHEN** a developer reads the sample startup code
- **THEN** it shows Azure Functions worker registration and MiniBus service registration in one coherent setup path

### Requirement: Sample demonstrates MiniBus processing registration
The Function App sample SHALL demonstrate registration for MiniBus Azure Functions processing, message serialization, handlers, recoverability options, and saga services when saga code is included.

#### Scenario: Developer inspects MiniBus registration
- **WHEN** a developer reads the sample MiniBus registration code
- **THEN** it shows endpoint name, recoverability settings, serializer registration, handler registration, and any included saga registration

### Requirement: Sample demonstrates Azure Service Bus transport setup
The Function App sample SHALL demonstrate Azure Service Bus route configuration and dispatcher service registration needed by `MiniBusContext` outgoing operations.

#### Scenario: Developer inspects transport registration
- **WHEN** a developer reads the sample transport setup
- **THEN** it shows command, event, or scheduled-message routes and required transport dispatcher dependencies

### Requirement: Sample includes handler-facing business code
The Function App sample SHALL include at least one MiniBus handler that processes a command and uses `MiniBusContext` for outgoing work.

#### Scenario: Handler uses MiniBusContext
- **WHEN** a developer reads the sample handler
- **THEN** the handler receives a MiniBus message, `MiniBusContext`, and `CancellationToken`, and requests outgoing work without Azure SDK dependencies

### Requirement: Sample documents configuration and limits
The Function App sample SHALL document how to build or inspect the sample and clearly identify intentionally omitted production concerns.

#### Scenario: Developer reads sample documentation
- **WHEN** a developer opens the sample documentation
- **THEN** it explains required local configuration placeholders, build commands, and that live Azure resources and first-class SQL Server persistence setup are outside this sample
