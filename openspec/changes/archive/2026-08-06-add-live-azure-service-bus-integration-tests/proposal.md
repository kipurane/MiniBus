## Why

MiniBus validates its Azure Service Bus behavior with unit tests and an emulator-backed reference workflow, but it does not prove that its primary production transport behaves correctly against the real Azure service. Live, opt-in integration coverage is now the highest-value production-confidence gap because routing, scheduling, settlement, dead-lettering, and broker metadata can differ from mocked or emulated behavior.

## What Changes

- Add an environment-gated live Azure Service Bus integration test suite that is excluded from the normal infrastructure-free test path unless explicitly selected.
- Create isolated, uniquely named queues, topics, and subscriptions inside a caller-supplied test namespace and clean them up on a best-effort basis.
- Verify routed send, publish, scheduled delivery, metadata preservation, completion, abandon/retry, dead-letter diagnostics, and deterministic outbox replay message IDs against Azure Service Bus.
- Add stable test traits/categories, bounded polling, explicit timeouts, parallel-safety rules, and clear distinctions between assertion failures and unavailable infrastructure.
- Document credentials, required namespace permissions, execution, expected cost, cleanup, troubleshooting, and optional manual/secret-gated CI execution.
- Update package-readiness guidance so live Azure Service Bus coverage is documented as available rather than deferred.

## Capabilities

### New Capabilities

- `live-azure-servicebus-integration-testing`: Defines opt-in live Azure Service Bus test configuration, resource isolation and lifecycle, covered production transport behaviors, selection, reliability, safety, and documentation.

### Modified Capabilities

- `package-readiness`: Updates root developer guidance to describe how live Azure Service Bus integration tests are configured and run instead of identifying them as future work.

## Impact

- Affected tests: Azure Service Bus transport and cross-package integration coverage, with the final project placement selected in the design.
- Affected dependencies: test-only use of `ServiceBusAdministrationClient` from the existing Azure Service Bus SDK dependency.
- Affected external systems: a dedicated, caller-managed Azure Service Bus namespace with data-plane and entity-management permissions.
- Affected documentation and automation: root development guidance plus an optional manually triggered or secret-gated CI workflow.
- Runtime packages and public runtime APIs remain unchanged.
