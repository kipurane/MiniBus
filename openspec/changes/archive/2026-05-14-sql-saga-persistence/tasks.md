## 1. Schema and Options

- [x] 1.1 Add `SagaTableName` to `MiniBusSqlPersistenceOptions` with default table name `Sagas`.
- [x] 1.2 Extend `SqlTableNames` to expose the quoted saga table name.
- [x] 1.3 Add an additive SQL schema script for the saga table, uniqueness constraint, indexes, timestamps, completion columns, serialized payload, and rowversion metadata.
- [x] 1.4 Include the saga schema script in package content using the existing schema script packaging pattern.

## 2. SQL Saga Persistence

- [x] 2.1 Add a SQL saga data serializer that stores and loads saga data using the configured MiniBus serializer behavior.
- [x] 2.2 Implement `SqlSagaPersistence.LoadAsync` to return deserialized saga data with encoded rowversion metadata or no record when missing.
- [x] 2.3 Implement `SqlSagaPersistence.CreateAsync` to insert saga data and reject duplicate saga data type plus correlation id records.
- [x] 2.4 Implement `SqlSagaPersistence.SaveAsync` to update existing saga data only when the expected rowversion matches.
- [x] 2.5 Implement `SqlSagaPersistence.CompleteAsync` to persist completed state, completion timestamp, serialized data, and concurrency metadata.
- [x] 2.6 Translate duplicate, missing, stale-version, serialization, and SQL write failures into clear `SagaPersistenceException` failures where appropriate.

## 3. Dependency Injection Integration

- [x] 3.1 Register `SqlSagaPersistence` as `ISagaPersistence` from `AddMiniBusSqlPersistence`.
- [x] 3.2 Ensure Azure Functions fallback saga persistence registration does not override an already configured saga persistence provider.
- [x] 3.3 Add unit coverage for service registration order so SQL saga persistence wins in common Azure Functions and SQL persistence registration orders.

## 4. SQL Server Verification

- [x] 4.1 Extend SQL Server integration test helpers to expose saga table assertions and schema application.
- [x] 4.2 Add integration tests for saga schema creation, create, load, save, and complete behavior.
- [x] 4.3 Add integration tests for duplicate create rejection, missing save rejection, and stale rowversion rejection.
- [x] 4.4 Add integration tests verifying saga data serialization round-trips reference-type properties.
- [x] 4.5 Run the SQL persistence test suite through the existing Testcontainers or external connection string path.

## 5. Documentation and Validation

- [x] 5.1 Update SQL persistence documentation or README guidance with SQL saga registration and schema script application.
- [x] 5.2 Document completion behavior, optimistic concurrency expectations, table-name configuration, and the application-owned migration policy.
- [x] 5.3 Run `dotnet test` for the affected projects and capture any SQL Server integration test prerequisites or skips.
- [x] 5.4 Run `openspec status --change sql-saga-persistence` and confirm the change is apply-ready.
