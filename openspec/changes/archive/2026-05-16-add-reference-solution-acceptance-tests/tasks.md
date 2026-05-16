## 1. Acceptance Test Structure

- [x] 1.1 Decide whether to add a new `tests/MiniBus.AcceptanceTests` project or place the scenarios in an existing test project with clear acceptance-test naming.
- [x] 1.2 Add the chosen test location to `MiniBus.sln` and reference the MiniBus packages, sample project, and test dependencies needed for the reference workflows.
- [x] 1.3 Add shared acceptance-test helpers for creating Service Bus received messages, recording settlement actions, and recording Azure Service Bus sends and schedules.
- [x] 1.4 Ensure the test helpers keep live Azure Service Bus, real Azure Functions host execution, and Docker out of the Tier 1 path.

## 2. Tier 1 Reference Solution Smoke Test

- [x] 2.1 Build the Tier 1 service provider using `samples/MiniBus.Samples.FunctionApp` registration directly or a close sample-style mirror.
- [x] 2.2 Override only infrastructure placeholders needed for testing, such as the sample transport sender and settlement actions, with recording test doubles.
- [x] 2.3 Create a realistic `CreateInvoice` `ServiceBusReceivedMessage` with MiniBus message type, message id, correlation id, and content metadata.
- [x] 2.4 Resolve `MiniBusProcessor` from the service provider and process the received billing message.
- [x] 2.5 Assert that the billing handler workflow publishes `InvoiceCreated`, sends `SendInvoiceReceipt`, schedules `InvoicePaymentTimeout`, and completes the received message.
- [x] 2.6 Verify the Tier 1 test runs as part of the normal infrastructure-free test suite.

## 3. Tier 2 SQL-Backed Reference Scenario

- [x] 3.1 Reuse or extract the existing SQL Server Testcontainers/external-connection-string fixture pattern for the acceptance test layer.
- [x] 3.2 Configure the reference workflow service provider with SQL persistence enabled and apply the SQL schema scripts to an isolated test schema or database.
- [x] 3.3 Process the same or equivalent billing message through `MiniBusProcessor` with SQL persistence enabled.
- [x] 3.4 Assert that the SQL inbox records the processed incoming message.
- [x] 3.5 Assert that outgoing send, publish, or schedule work is captured in the SQL outbox as part of the successful processing transaction.
- [x] 3.6 Assert saga state or saga timeout scheduling is durably represented according to the chosen SQL-backed workflow shape.
- [x] 3.7 Preserve clear skip behavior when neither Docker nor the documented SQL Server/Azure SQL test connection string is available.

## 4. Documentation

- [x] 4.1 Add a brief README or test documentation note explaining the reference solution acceptance-test layer.
- [x] 4.2 Document that Tier 1 is always-on and infrastructure-free.
- [x] 4.3 Document that the SQL-backed Tier 2 scenario uses the same Docker/Testcontainers or external connection string behavior as SQL integration tests.

## 5. Verification

- [x] 5.1 Run the Tier 1 acceptance test without Docker or live Azure resources.
- [x] 5.2 Run the SQL-backed Tier 2 scenario with Testcontainers if Docker is available.
- [x] 5.3 Run the normal relevant test suite for changed test projects.
- [x] 5.4 Run OpenSpec validation for `add-reference-solution-acceptance-tests`.
