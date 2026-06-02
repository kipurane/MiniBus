# Changelog

All notable MiniBus package changes should be recorded here before release.

## Unreleased

### Breaking Changes

- `ISagaPersistence.SaveAsync` and `ISagaPersistence.CompleteAsync` now require a non-null, non-whitespace `string version` concurrency token. The token is returned by `LoadAsync` and must be passed back unchanged when saving or completing existing saga data. Implementations should reject missing or malformed tokens with `ArgumentException` and report stale or missing saga state with `SagaPersistenceException`.

  Downstream `ISagaPersistence` implementations must update their method signatures from `string? version` to `string version` and remove any blind update behavior that treated `null` as "skip optimistic concurrency." Callers that save or complete saga state directly must first load the saga data and use the returned version token.

  This change is part of the saga processing reliability boundary work. It must ship in a breaking release line. If MiniBus is still pre-1.0 when released, call it out prominently in the preview release notes; if MiniBus has reached 1.0 or later, release it in the next major version.

### Deprecated

- `SqlMiniBusPersistenceSessionFactory` constructors that accept `SqlOutboxOperationSerializer` without an explicit `SqlSagaDataSerializer` are obsolete. Use the `IMessageSerializer` constructors to share one serializer for outbox operations and saga data, or the explicit serializer constructor when saga serialization intentionally differs.
