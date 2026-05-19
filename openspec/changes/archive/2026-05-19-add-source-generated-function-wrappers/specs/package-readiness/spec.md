## ADDED Requirements

### Requirement: Source generator package is distributable
MiniBus SHALL define package metadata and package documentation for the Azure Functions source generator package when it is introduced as a distributable package.

#### Scenario: Source generator package is packed
- **WHEN** the Azure Functions source generator project is packed
- **THEN** the generated package contains package metadata, README content, repository metadata, license metadata, and tags consistent with the other distributable MiniBus packages

#### Scenario: Source generator package is consumed
- **WHEN** a developer reads the source generator package README
- **THEN** it explains how to reference the package, declare generated wrappers, and keep manual wrappers as a supported alternative

#### Scenario: Runtime packages are packed
- **WHEN** MiniBus runtime packages are packed
- **THEN** they do not include Roslyn source generator implementation dependencies as runtime dependencies
