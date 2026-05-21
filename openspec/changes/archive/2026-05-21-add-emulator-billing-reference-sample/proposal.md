## Why

MiniBus has a buildable Billing sample that demonstrates the intended Azure Functions and Azure Service Bus shape, but it stops short of a local runnable workflow because outgoing dispatch uses a placeholder sender. The next sample increment should let developers exercise that reference path against the Azure Service Bus emulator before the project expands to multi-endpoint samples or live Azure Service Bus proof coverage.

## What Changes

- Turn the existing Billing sample into an emulator-first local reference workflow instead of only a compile-time setup example.
- Add repository-owned local run guidance and emulator assets for the Billing queues, topic subscription, and scheduled timeout route that the sample depends on.
- Replace the sample's placeholder outgoing transport path for emulator execution with the real Azure Service Bus sender and connection configuration needed by MiniBus dispatch.
- Provide a local way to submit the initial Billing command and observe command handling, outgoing send/publish work, and Billing event processing.
- Validate the emulator-backed Azure Functions flow before promising saga timeout scheduling as part of the runnable workflow; document any emulator limitations that remain relevant to the sample.
- Keep Inventory, broader multi-endpoint expansion, general Azure infrastructure provisioning, and live Azure Service Bus integration coverage out of this change.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `buildable-functionapp-sample`: Extend the existing Billing sample contract from buildable reference code to an emulator-runnable local reference workflow with setup, seeding, and verification guidance.
- `azure-functions-adapter`: Strengthen the sample expectations for a locally runnable Functions-facing reference path while keeping Azure Functions wrappers thin.
- `azure-servicebus-transport`: Strengthen the sample expectations for real local Service Bus dispatch configuration used by `Send`, `Publish`, and scheduled work in the emulator-backed sample.

## Impact

- Sample code and local run documentation under `samples/MiniBus.Samples.FunctionApp`.
- Azure Service Bus emulator configuration and local workflow assets required by the Billing reference path.
- Sample-facing OpenSpec coverage for the Function App sample, Azure Functions adapter, and Azure Service Bus transport.
- Local verification for the emulator-backed sample workflow without adding live Azure Service Bus infrastructure requirements.
