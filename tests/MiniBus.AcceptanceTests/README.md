# MiniBus Acceptance Tests

This project contains high-level reference solution tests. These tests are a small canary layer above the unit, adapter, transport, SQL, and Azure Storage suites.

Tier 1 tests are always-on and infrastructure-free. They build a real service provider from sample-style MiniBus registration, use recording transport and settlement doubles, and process a realistic billing workflow without Docker, live Azure Service Bus, or a real Azure Functions host.

Tier 2 tests verify one SQL-backed reference workflow. They use the same SQL Server Testcontainers path as the SQL persistence integration tests, or `MINIBUS_SQLSERVER_TEST_CONNECTION_STRING` when an external SQL Server/Azure SQL database should be used. If neither Docker nor the external connection string is available, SQL-backed acceptance tests skip with a clear reason.
