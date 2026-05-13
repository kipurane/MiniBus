## 1. Schema and Options

- [x] 1.1 Add SQL persistence options for outbox claim lease duration, cleanup retention windows, and cleanup batch size with conservative defaults.
- [x] 1.2 Add an additive SQL schema script for deterministic outgoing message ids and any indexes needed for replay and cleanup.
- [x] 1.3 Ensure packaged SQL scripts are included in the NuGet content layout in version order.
- [x] 1.4 Document the script-based migration policy and the default-name limitation for packaged scripts.

## 2. Transaction Ownership

- [x] 2.1 Design the public API for committing MiniBus SQL persistence state inside an application-owned `DbConnection` and `DbTransaction`.
- [x] 2.2 Implement the application-owned transaction commit path without committing, rolling back, or disposing caller-owned transaction resources.
- [x] 2.3 Preserve the existing MiniBus-owned transaction path for normal Azure Functions processing.
- [x] 2.4 Add validation for closed connections, missing transactions, and mismatched connection/transaction ownership.

## 3. Deterministic Outbox Identity

- [x] 3.1 Add a stored outgoing message id to the SQL outbox model and serialization/dispatch path.
- [x] 3.2 Generate distinct deterministic outgoing message ids for multiple operations captured from the same incoming message.
- [x] 3.3 Ensure outbox dispatch reuses the stored outgoing message id on every retry or replay attempt.
- [x] 3.4 Update Azure Service Bus outbox dispatch mapping if needed so the stored outgoing message id becomes the transport message id.

## 4. Crash Recovery and Cleanup

- [x] 4.1 Replace the hard-coded outbox claim timeout with the configured claim lease duration.
- [x] 4.2 Verify claimed rows remain unavailable until the lease expires and become claimable after expiry.
- [x] 4.3 Add SQL cleanup operations for expired inbox records and dispatched outbox records.
- [x] 4.4 Keep failed or undispatched outbox records unless a separate failed-retention policy is configured.
- [x] 4.5 Add cleanup batch limiting so large tables can be cleaned incrementally.

## 5. SQL Server Integration Tests

- [x] 5.1 Add SQL Server-backed tests for application-owned transaction commit and rollback with business data plus inbox/outbox records.
- [x] 5.2 Add SQL Server-backed tests for deterministic outgoing message id generation and dispatch replay stability.
- [x] 5.3 Add SQL Server-backed tests for claim lease recovery after an abandoned claim.
- [x] 5.4 Add SQL Server-backed tests for inbox cleanup, dispatched outbox cleanup, failed outbox retention, and cleanup batch limits.
- [x] 5.5 Ensure the new integration tests run through the existing Testcontainers path and external connection string override.

## 6. Documentation and Verification

- [x] 6.1 Update README and SQL persistence documentation for transaction sharing, deterministic outbox ids, claim lease behavior, cleanup options, and schema scripts.
- [x] 6.2 Update sample guidance only where it helps developers choose the default MiniBus-owned path or the advanced shared-transaction path.
- [x] 6.3 Run the normal .NET test suite.
- [x] 6.4 Run SQL Server-backed integration tests through Testcontainers.
- [x] 6.5 Run OpenSpec validation for `complete-sqlserver-azure-sql-persistence`.
