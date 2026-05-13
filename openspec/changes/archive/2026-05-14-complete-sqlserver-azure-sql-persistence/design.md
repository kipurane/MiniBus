## Context

`MiniBus.Persistence.Sql` already provides SQL Server/Azure SQL registration through `Microsoft.Data.SqlClient`, provider-neutral `DbConnection` factory registration, inbox duplicate detection, outbox capture, outbox dispatch metadata, and Testcontainers-backed SQL Server integration tests.

The remaining production gaps are narrower than the original "add SQL Server support" milestone: the package needs an explicit transaction-sharing model for business data, replay-safe outbox identities, configurable crash recovery for claimed outbox rows, cleanup policies, and a stable schema evolution policy.

## Goals / Non-Goals

**Goals:**

- Support MiniBus-owned transactions and application-owned SQL transactions without making handlers depend on MiniBus persistence internals.
- Store deterministic outgoing message ids for outbox rows so repeated dispatch attempts for the same row use the same transport message identity.
- Make outbox claim expiry configurable and verify that abandoned claims are retried safely.
- Add configurable cleanup for inbox records and dispatched/failed outbox records.
- Keep schema management explicit by shipping SQL scripts that applications apply through their deployment process.
- Extend SQL Server/Azure SQL integration tests to cover the new guarantees.

**Non-Goals:**

- SQL saga persistence.
- Azure Storage persistence.
- Runtime database migration execution owned by MiniBus.
- Manual retry dashboards or operational tooling.
- Automatic Azure infrastructure provisioning.

## Decisions

### Keep script-based schema management

MiniBus will ship explicit SQL scripts as package content. Applications remain responsible for applying scripts through their normal database deployment flow. New schema needs, such as stored outgoing message ids or cleanup-friendly indexes, should be delivered as additive versioned scripts rather than silently changing the meaning of an existing script.

Alternative considered: add a MiniBus runtime migrator. That would make first-run demos easier, but it would also make production database ownership ambiguous and would pull the framework toward deployment tooling rather than message processing.

### Model transaction sharing as explicit application ownership

The default path remains MiniBus-owned: the persistence session opens its connection, begins a transaction, commits inbox/outbox state, and rolls back on failure. For applications that need business data and MiniBus persistence in the same SQL transaction, add an explicit application-owned path that uses a caller-provided open connection and active transaction for the commit operation. MiniBus should not commit or roll back an application-owned transaction; it should only execute its persistence commands inside it and surface failures.

Alternative considered: rely on ambient `TransactionScope`. That can work in some .NET applications, but it is less explicit, can trigger distributed transaction behavior, and is a poor default fit for Azure Functions.

### Store deterministic outbox message identity

Each persisted outbox operation should have a stable outgoing message id derived at capture time from the endpoint, incoming message id, operation sequence, and operation kind/message metadata, or an equivalent deterministic input set. The id should be stored with the outbox row and reused by every dispatch attempt. This is separate from the outbox row primary key, which can remain an implementation identity.

Alternative considered: keep using random transport ids per dispatch attempt. That is simpler, but it weakens idempotency expectations when dispatch crashes after sending and before marking the row as dispatched.

### Make claim lease expiry explicit and configurable

Outbox dispatch already treats old claims as retryable after five minutes. This change should expose that as an option with a conservative default and use it consistently in claim queries. Marking failures should continue to clear the claim immediately. A process crash should leave the claim in place until the lease expires, at which point another dispatcher can reclaim the row.

Alternative considered: clear claims through a separate recovery job. That adds operational moving parts without improving the basic crash recovery guarantee.

### Add cleanup as bounded retention, not automatic deletion surprises

Cleanup should be explicit and configurable by retention windows and batch size. Inbox cleanup should remove old processed-message records only after the configured deduplication retention period. Outbox cleanup should remove dispatched rows after their retention period and optionally remove permanently failed rows only when a separate failed retention policy is configured.

Alternative considered: automatically delete records immediately after dispatch. That reduces table growth, but removes useful audit/debug data and makes replay diagnostics harder.

## Risks / Trade-offs

- [Risk] Application-owned transactions can be misused by passing a closed connection or a transaction from a different connection. -> Mitigation: validate inputs and fail before writing.
- [Risk] Deterministic ids can collide if based on insufficient inputs. -> Mitigation: include endpoint, incoming message id, operation sequence, operation kind, and message type, and cover collision behavior with tests.
- [Risk] Claim lease retry can duplicate a transport send if the original dispatcher sent but crashed before marking dispatched. -> Mitigation: stable outgoing message ids let downstream idempotency and broker duplicate detection recognize the replay where configured.
- [Risk] Cleanup can remove inbox deduplication records too early. -> Mitigation: require explicit retention settings and document the relationship to maximum retry/replay windows.
- [Risk] Schema scripts can drift from custom table/schema names. -> Mitigation: keep packaged scripts for default names and document custom-name responsibility until a script generation story is intentionally designed.

## Migration Plan

1. Add any required additive SQL script, such as a new `OutgoingMessageId` column and supporting indexes.
2. Keep existing registration APIs source compatible.
3. Make new options default to current behavior where possible, including MiniBus-owned transactions and a five-minute outbox claim lease.
4. Document how existing databases apply the new script before enabling deterministic outbox dispatch and cleanup.
5. Rollback by disabling cleanup execution and reverting application usage of shared-transaction APIs; schema additions can remain harmless if unused.

## Open Questions

- Should deterministic outgoing message ids be GUID-shaped for transport compatibility, string-shaped for readability, or expose both a stored string and a transport-specific conversion?
- Should cleanup be exposed as store methods only, or should `MiniBus.Persistence.Sql` also provide a hosted cleanup service for worker-style hosts?
