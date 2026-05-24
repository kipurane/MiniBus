## MODIFIED Requirements

### Requirement: Function App sample is buildable
MiniBus SHALL provide a buildable Azure Functions isolated worker Billing sample project under `samples/MiniBus.Samples.Billing.FunctionApp`.

#### Scenario: Sample project builds with the solution
- **WHEN** the solution is built or tested
- **THEN** the Billing sample project compiles against the current MiniBus project references

#### Scenario: Sample project is included in the solution
- **WHEN** a developer opens `MiniBus.sln`
- **THEN** the Billing Function App sample appears as `MiniBus.Samples.Billing.FunctionApp` rather than only loose solution items

#### Scenario: Billing sample uses endpoint-specific identity
- **WHEN** a developer inspects the Billing sample directory, project file, assembly output, or root namespace
- **THEN** each uses the endpoint-specific `MiniBus.Samples.Billing.FunctionApp` identity instead of the generic `MiniBus.Samples.FunctionApp` identity

#### Scenario: Billing and Inventory sample names align
- **WHEN** a developer compares the sample endpoint projects
- **THEN** Billing uses `MiniBus.Samples.Billing.FunctionApp` and Inventory uses `MiniBus.Samples.Inventory.FunctionApp`
