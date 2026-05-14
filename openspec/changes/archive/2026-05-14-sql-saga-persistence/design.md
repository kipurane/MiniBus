## Context

MiniBus already has `ISagaPersistence`, `SagaInvoker`, and `InMemorySagaPersistence` in `MiniBus.Core`, plus `MiniBus.Persistence.Sql` for SQL inbox/outbox storage. The current saga persistence abstraction loads by saga data type and correlation id, creates new saga data, saves existing data with version metadata, and marks saga data complete. Production workloads need the same contract backed by SQL Server / Azure SQL so saga state survives restarts and concurrent function instances can detect conflicting updates.

The SQL persistence package already owns SqlClient registration, schema scripts, table-name quoting, integration tests, and the policy that applications apply SQL scripts themselves. SQL saga persistence should extend that package without introducing a separate storage dependency or changing handler-facing saga APIs.

## Goals / Non-Goals

**Goals:**

- Implement `ISagaPersistence` in `MiniBus.Persistence.Sql` using SQL Server / Azure SQL.
- Store saga data durably by saga data type and correlation id with optimistic concurrency metadata.
- Preserve completed-saga terminal behavior by persisting `IsCompleted` and completion timestamps.
- Add an additive SQL schema script for saga storage.
- Register SQL saga persistence through the existing SQL persistence DI extension.
- Verify behavior with SQL Server-compatible integration tests.

**Non-Goals:**

- Redesign the core saga API or saga correlation model.
- Add runtime schema migration execution.
- Provide non-SQL saga persistence providers.
- Add transactional coupling between saga state and SQL inbox/outbox in this change unless an existing processing transaction can be reused without changing public contracts.
- Encrypt saga data at rest beyond the database/application's existing storage configuration.

## Decisions

### Key saga rows by data type and correlation id

`ISagaPersistence` does not receive the saga class type, only `TData` and `correlationId`. The SQL table will therefore use the saga data type identity plus correlation id as the natural uniqueness boundary, with the saga data `Id` stored as the row id.

Alternative considered: change `ISagaPersistence` and `SagaInvoker` to pass the saga type. That would make stored metadata richer, but it creates a broader core contract change for little behavioral value because one saga data type already represents one saga state shape.

### Store serialized saga data as the source of truth

The provider will serialize the whole saga data object into a binary payload and store type metadata, correlation id, completion state, and timestamps as queryable columns. This keeps provider behavior generic across arbitrary saga data classes while still making duplicate, load, save, and completion checks efficient.

Alternative considered: map saga data properties into columns. That would require user mappings or reflection-driven schema generation, which does not match MiniBus's small explicit persistence model.

### Use SQL Server rowversion for optimistic concurrency

The saga table will include a `rowversion` column. `LoadAsync` returns the rowversion encoded as a string, while `SaveAsync` and `CompleteAsync` update with a `WHERE` predicate that includes the expected rowversion when one is provided. Zero affected rows become a `SagaPersistenceException` indicating missing or stale saga state.

Alternative considered: manage a numeric version column in application code. A rowversion avoids read-modify-write races and uses SQL Server's native concurrency primitive.

### Add a dedicated saga schema script

Saga storage will ship as a new additive script, for example `003-sagas.sql`, targeting the default `MiniBus.Sagas` table. `MiniBusSqlPersistenceOptions` will gain a `SagaTableName` option and `SqlTableNames` will quote it consistently with inbox and outbox names.

Alternative considered: append saga objects to an existing inbox/outbox script. A dedicated script keeps previously shipped scripts stable and matches the current explicit migration policy.

### Register SQL saga persistence as the configured saga provider

`AddMiniBusSqlPersistence` will register `ISagaPersistence` to the SQL implementation. The Azure Functions fallback registration should not overwrite an already configured saga persistence provider, so the fallback should use `TryAdd` semantics or equivalent ordering-safe behavior.

Alternative considered: require applications to register `ISagaPersistence` manually. That leaves the feature easy to misconfigure and does not match the existing first-class SQL persistence registration path.

## Risks / Trade-offs

- [Risk] SQL registration order could still leave `UnconfiguredSagaPersistence` active. -> Mitigation: make fallback saga persistence registration non-overwriting and cover both common registration orders in tests.
- [Risk] Saga data type names can change between deployments. -> Mitigation: store assembly-qualified type metadata for diagnostics while loading by `TData`; document that renames may require application-managed data migration.
- [Risk] Serialized saga payloads are opaque to SQL queries. -> Mitigation: this provider optimizes durable framework state, not reporting; applications that need queryable workflow views should maintain business projections separately.
- [Risk] Concurrency conflicts may surface during scale-out handling. -> Mitigation: translate duplicate creates and stale rowversion updates into `SagaPersistenceException` so processing can fail clearly and retry through existing recoverability.
- [Risk] Large saga payloads can affect SQL storage and throughput. -> Mitigation: document that saga data should remain workflow state, not large document storage.

## Migration Plan

Applications that enable SQL saga persistence will apply the new saga schema script during deployment, then register `AddMiniBusSqlPersistence` with the same connection settings used for inbox/outbox. Existing applications that do not enable sagas or do not apply the new script keep current behavior.

Rollback is application-owned: stop using SQL saga persistence before rolling back the script. Since the script is additive, existing inbox/outbox tables are unaffected.

## Open Questions

- Should the initial SQL saga provider participate in the same SQL transaction as inbox/outbox persistence during message processing, or should that be proposed separately as a broader processing transaction change?
