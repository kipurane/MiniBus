## Why

The current sample files are useful snippets, but they are not a buildable Azure Functions sample application. A minimal working sample should be added now so MiniBus API design is exercised by a realistic host setup while the framework is still early enough to adjust.

## What Changes

- Add a buildable `samples/MiniBus.Samples.FunctionApp` project.
- Include a minimal Azure Functions isolated worker host configuration.
- Demonstrate registering MiniBus Azure Functions processing.
- Demonstrate Azure Service Bus transport routing and dispatcher registration.
- Demonstrate a simple command handler using `MiniBusContext`.
- Demonstrate recoverability configuration.
- Keep saga usage either minimal or in a clearly separated example.
- Keep SQL persistence optional and documented as a follow-up until first-class SQL Server/Azure SQL support is added.
- Add the sample project to `MiniBus.sln`.
- Add documentation explaining how to inspect, configure, and run the sample.

## Capabilities

### New Capabilities

- `buildable-functionapp-sample`: A working Azure Functions sample app that demonstrates the currently stable MiniBus setup path.

### Modified Capabilities

- `azure-functions-adapter`: Documentation/sample coverage shows complete isolated worker registration and function wrapper usage.
- `azure-servicebus-transport`: Documentation/sample coverage shows routing and transport dispatcher registration.

## Impact

- New or updated files under `samples/MiniBus.Samples.FunctionApp`.
- Updates to `MiniBus.sln`.
- Possible README updates for sample discovery.
- No production framework behavior changes expected.
- The sample should avoid depending on unfinished SQL persistence provider packaging.
