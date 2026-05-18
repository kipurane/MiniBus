# MiniBus.Persistence.AzureStorage

`MiniBus.Persistence.AzureStorage` provides Azure Blob Storage persistence for MiniBus payload storage, claim-check/DataBus behavior, and audit blob writing.

It includes:

- `BlobMiniBusPayloadStore` for storing opaque MiniBus payload bytes in Blob Storage.
- `AddMiniBusAzureBlobClaimCheck` for replacing above-threshold outgoing payloads with compact claim-check messages.
- Receive-side claim-check resolution through the MiniBus Azure Functions processing pipeline.
- `BlobMiniBusAuditWriter` for optional processed-message audit records.

## Payload store and claim-check

Register Blob payload persistence first, then opt in to claim-check behavior:

```csharp
services.AddMiniBusAzureStoragePersistence(
    connectionString,
    containerName: "minibus-payloads",
    options =>
    {
        options.BlobNamePrefix = "payloads";
        options.PayloadRetention = TimeSpan.FromDays(7);
    })
    .AddMiniBusAzureBlobClaimCheck(payloadThresholdBytes: 128 * 1024);
```

Messages at or below the threshold stay inline. Larger outgoing `Send`, `Publish`, and `Schedule` bodies are written to Blob Storage and replaced with compact MiniBus claim-check metadata. The Azure Functions processor resolves claim-check payloads before deserialization, so handlers and sagas still receive the original message contract.

Configure payload retention to exceed the longest expected processing window, including delayed retries, scheduled delivery, and SQL outbox replay.

## Audit blobs

Register audit writing separately:

```csharp
services.AddMiniBusAzureBlobAudit(
    connectionString,
    auditContainerName: "minibus-audits",
    options =>
    {
        options.AuditBlobNamePrefix = "audits";
        options.AuditRetention = TimeSpan.FromDays(30);
    });
```

Audit records are written before final completion or dead-letter settlement. They include message identity, correlation metadata, headers, outcome, retry/dead-letter metadata, and inline body or claim-check reference context. Audit write failures are fail-closed: MiniBus treats them as processing failures rather than silently completing messages expected to be audited.

Use the options overload when the application owns Azure SDK client configuration. With the default registrations, MiniBus normally reuses the resulting `BlobContainerClient` for the service provider lifetime.

`MiniBus.Persistence.AzureStorage.Tests` can run Blob Storage integration coverage through Testcontainers-backed Azurite when Docker is available, or against a live storage account when `MINIBUS_AZURE_STORAGE_TEST_CONNECTION_STRING` is set.

Automatic Azure Storage provisioning is future work.
