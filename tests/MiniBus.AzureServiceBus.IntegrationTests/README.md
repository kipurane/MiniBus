# MiniBus live Azure Service Bus integration tests

This project verifies representative MiniBus transport, scheduling, settlement, recoverability, and outbox replay behavior against the production Azure Service Bus service. It is separate from the fast unit tests and the repo-owned emulator workflow.

Live transport coverage includes stable message identity metadata through direct delivery and a scheduled delayed retry.

## Prerequisites

Use a dedicated test namespace rather than an application namespace. The connection string must allow:

- sending messages;
- receiving and settling messages;
- creating and deleting queues, topics, and subscriptions.

The suite creates temporary entities whose names start with `minibus-live`. Azure charges for the namespace tier and messaging operations while the namespace exists; consult the Azure pricing for the selected tier before enabling recurring runs.

## Run

Live execution requires both an explicit opt-in and the namespace connection string:

```bash
export MINIBUS_RUN_LIVE_AZURE_SERVICEBUS_TESTS=true
export MINIBUS_AZURE_SERVICEBUS_CONNECTION_STRING='<dedicated test namespace connection string>'

dotnet test tests/MiniBus.AzureServiceBus.IntegrationTests/MiniBus.AzureServiceBus.IntegrationTests.csproj \
  --filter Category=LiveAzureServiceBus
```

Without both variables, live tests skip with a clear reason. Infrastructure-free helper tests still run, and building the solution never contacts Azure Service Bus.

## Isolation, timing, and cleanup

Each scenario creates uniquely named queues, topics, and subscriptions. Independent scenarios can run in parallel. Receives and state observations use bounded polling with explicit deadlines; scheduled delivery uses a tolerance window because Azure delivery and clocks are not exact.

Fixtures delete created subscriptions, topics, and queues in dependency-safe order. Cleanup is best effort so a cleanup error does not hide the primary assertion failure. If a process is terminated before disposal:

1. Open the dedicated namespace in Azure Portal or Service Bus Explorer.
2. Locate entities whose names start with `minibus-live`.
3. Confirm no active test run owns them.
4. Delete subscriptions before their topics, then delete the topics and queues.

## Failure and secret handling

Only missing opt-in or missing configuration causes a skip. Once live execution is enabled, invalid credentials, insufficient permissions, network failures, Azure errors, and broker timeouts fail the test so broken infrastructure is visible.

The suite does not print connection strings or shared access keys. Keep the connection string in a local secret store or protected CI environment and do not enable verbose environment dumps.

The manual GitHub Actions workflow uses the protected `MINIBUS_AZURE_SERVICEBUS_CONNECTION_STRING` secret. Ordinary pull-request validation does not require or run live Azure tests.
