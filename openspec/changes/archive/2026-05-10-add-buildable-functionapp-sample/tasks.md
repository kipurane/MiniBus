## 1. Sample Project Setup

- [x] 1.1 Add `samples/MiniBus.Samples.FunctionApp/MiniBus.Samples.FunctionApp.csproj` targeting the solution runtime and Azure Functions isolated worker.
- [x] 1.2 Reference the local MiniBus projects needed by the sample.
- [x] 1.3 Add the sample project to `MiniBus.sln` as a buildable project while preserving existing sample solution items if needed.

## 2. Function Host and Configuration

- [x] 2.1 Add a minimal `Program.cs` for Azure Functions isolated worker startup.
- [x] 2.2 Update `MiniBusFunctionAppConfiguration` to register `IMessageSerializer`, MiniBus Azure Functions processing, recoverability, handlers, and optional saga services.
- [x] 2.3 Add Azure Service Bus route configuration for all outgoing messages used by sample handlers.
- [x] 2.4 Register required Azure Service Bus transport dispatcher dependencies using sample-safe placeholders or documented configuration boundaries.

## 3. Sample Messages and Handlers

- [x] 3.1 Add clear sample command and event contracts.
- [x] 3.2 Add at least one regular `IHandleMessages<TMessage>` handler that uses `MiniBusContext` for outgoing work.
- [x] 3.3 Keep the existing saga example compiling, or move it into a clearly separated optional sample area.
- [x] 3.4 Keep the manual Service Bus trigger wrapper thin and delegating to `MiniBusProcessor`.

## 4. Documentation and Verification

- [x] 4.1 Add sample README or root README section explaining what the sample demonstrates.
- [x] 4.2 Document required local settings placeholders and note that live Azure resources are not provisioned by the sample.
- [x] 4.3 Document that SQL persistence is intentionally not wired until first-class SQL Server/Azure SQL support lands.
- [x] 4.4 Verify the sample project builds with the solution.
- [x] 4.5 Run the test suite after adding the sample project.
