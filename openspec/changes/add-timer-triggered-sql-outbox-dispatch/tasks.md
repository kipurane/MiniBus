## 1. Timer Dispatch Reference Shape

- [ ] 1.1 Decide the sample project layout for a separate timer-triggered SQL outbox dispatcher Function App, including shared Billing configuration reuse and local settings.
- [ ] 1.2 Add a timer-triggered Function that resolves `SqlMiniBusOutboxDispatcher` from dependency injection and executes a bounded drain without duplicating SQL outbox claim or dispatch logic.
- [ ] 1.3 Keep the existing manual drain command available for local troubleshooting and scripted acceptance paths unless the implementation deliberately replaces it.

## 2. Configuration And Documentation

- [ ] 2.1 Document the recommended separate dispatcher Function App shape and the acceptable colocated timer-trigger shape.
- [ ] 2.2 Document timer cadence, bounded drain limits, scale-out safety, at-least-once delivery, and claim-lease recovery behavior for Azure Functions users.
- [ ] 2.3 Update the SQL persistence documentation to show manual, hosted-service, and timer-triggered dispatch as distinct scheduling choices over the same dispatcher.

## 3. Verification

- [ ] 3.1 Add build or registration tests proving the dispatcher Function App sample composes SQL persistence, transport dispatch, and timer-triggered drain dependencies.
- [ ] 3.2 Add infrastructure-light tests around the timer drain method so normal test runs can verify bounded dispatch behavior without a live Azure Functions host.
- [ ] 3.3 Extend emulator or acceptance guidance if the dispatcher Function App becomes part of the local reference workflow.
