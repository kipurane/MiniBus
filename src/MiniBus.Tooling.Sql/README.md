# MiniBus.Tooling.Sql

SQL Server/Azure SQL provider for MiniBus operational tooling.

This package maps the existing MiniBus SQL inbox, outbox, and saga tables into `MiniBus.Tooling.Core` read models. It supports local troubleshooting filters over endpoint, message id, correlation id, status, and time windows where the SQL state can answer those filters.

The bounded outbox drain action wraps the existing `SqlMiniBusOutboxDispatcher` so dispatch semantics stay centralized in `MiniBus.Persistence.Sql`.
