# MiniBus Acceptance Tests

This project contains high-level reference solution tests. These tests are a small canary layer above the unit, adapter, transport, SQL, and Azure Storage suites.

Tier 1 tests are always-on and infrastructure-free. They build real service providers from sample-style MiniBus registration, use recording transport and settlement doubles, and process a realistic billing workflow that dispatches to Inventory without Docker, live Azure Service Bus, or a real Azure Functions host.

Tier 2 tests verify the SQL-backed Billing reference workflow, including durable inbox/outbox/saga effects, duplicate inbox delivery handling, and outbox draining through the configured transport abstraction. They use the same SQL Server Testcontainers path as the SQL persistence integration tests, or `MINIBUS_SQLSERVER_TEST_CONNECTION_STRING` when an external SQL Server/Azure SQL database should be used. If neither Docker nor the external connection string is available, SQL-backed acceptance tests skip with a clear reason.

The Service Bus emulator acceptance tests verify the emulator-backed Billing and Inventory reference workflow when the Billing sample emulator is already running. The SQL-backed emulator scenario also uses the SQL endpoint exposed by that compose stack on `localhost:14333`, applies the packaged MiniBus SQL schema scripts, and drains Billing outbox work before checking transport-visible messages. The tests skip when the emulator is not reachable on `localhost:5672`; set `MINIBUS_SERVICEBUS_EMULATOR_CONNECTION_STRING` when the emulator is exposed through another connection string. Message receive waits default to 10 seconds; set `MINIBUS_SERVICEBUS_EMULATOR_RECEIVE_TIMEOUT_SECONDS` when a slower emulator host needs more time.
