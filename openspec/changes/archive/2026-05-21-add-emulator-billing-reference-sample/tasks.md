## 1. Emulator Workflow Boundaries

- [x] 1.1 Validate the Azure Service Bus emulator path that the Billing reference workflow will promise, including queue-trigger processing, topic/subscription processing, and scheduled-timeout dispatch or its documented local limitation.
- [x] 1.2 Add repository-owned Azure Service Bus emulator topology assets for the Billing command queue, receipt destination, domain events topic/subscription, and timeout destination used by the sample routes.

## 2. Runnable Billing Function App

- [x] 2.1 Promote `samples/MiniBus.Samples.FunctionApp` into a complete runnable isolated-worker Azure Functions host while preserving readable Billing registration, thin wrappers, handlers, and saga code.
- [x] 2.2 Replace the emulator execution path's throwing sample sender with configuration-backed `ServiceBusClient` and `AzureServiceBusSender` registration for MiniBus outgoing dispatch.
- [x] 2.3 Add the local Functions and Service Bus configuration files or settings needed for the Billing sample to connect to the emulator-owned topology.

## 3. Local Reference Workflow

- [x] 3.1 Add a repository-owned way to submit the initial `CreateInvoice` Billing command to the emulator with the MiniBus message metadata expected by the receive pipeline.
- [x] 3.2 Document how to start the emulator workflow, run the Billing Function App, submit the initial command, observe send/publish and Billing event processing, and understand validated timeout behavior and emulator limitations.

## 4. Verification

- [x] 4.1 Update build or repository verification so the runnable Billing sample host and its local workflow assets remain buildable without requiring live Azure Service Bus infrastructure.
- [x] 4.2 Add emulator-backed verification for the documented local Billing workflow when the required local emulator infrastructure is available.
- [x] 4.3 Verify the sample-facing Azure Functions and Azure Service Bus guidance matches the new emulator-backed reference workflow while keeping live Azure Service Bus coverage out of this change.
