# MiniBus.Tooling.Web

Read-only browser tooling for local MiniBus operational troubleshooting.

`MiniBus.Tooling.Web` packages an ASP.NET Core Minimal API and a React/TypeScript UI in one web app. The first slice is intentionally read-only: it lists inbox, outbox, and saga records, shows record details, and renders best-effort message or correlation timelines from configured tooling providers.

## Configuration

The first provider is SQL-backed tooling through `MiniBus.Tooling.Sql`:

```json
{
  "MiniBus": {
    "Tooling": {
      "Sql": {
        "ConnectionString": "Server=localhost,1433;Database=MiniBus;User Id=sa;Password=your-password;TrustServerCertificate=True;Encrypt=True",
        "SchemaName": "MiniBus"
      }
    }
  }
}
```

Connection strings and credentials remain application-owned. The web app does not print them in API responses or UI output.

## API

The read-only API lives under `/api/tooling`:

- `GET /api/tooling/inbox`
- `GET /api/tooling/inbox/{messageId}`
- `GET /api/tooling/outbox`
- `GET /api/tooling/outbox/{messageId}`
- `GET /api/tooling/sagas`
- `GET /api/tooling/sagas/{correlationId}`
- `GET /api/tooling/timeline/message/{messageId}`
- `GET /api/tooling/timeline/correlation/{correlationId}`

Common query parameters are `endpoint`, `messageId`, `correlationId`, `status`, `from`, `to`, and `limit` where the underlying provider supports them.

## Safety

The first web slice does not expose outbox drain, retry, DLQ resubmit, message replay, destructive broker operations, or other state-changing actions. Full message bodies, full saga data, and credentials are not shown by default.

Aspire local orchestration for the reference stack lives under `samples/MiniBus.Samples.AppHost`. Aspire is not a runtime dependency of this package.
