using System.Net.Sockets;
using System.Text;
using Azure.Storage.Blobs;
using Testcontainers.Azurite;
using Xunit;

namespace MiniBus.Persistence.AzureStorage.Tests;

public sealed class AzureStoragePayloadStoreIntegrationTests :
    IClassFixture<AzureStoragePayloadStoreIntegrationTests.AzureStorageFixture>
{
    private const string ConnectionStringEnvironmentVariable = "MINIBUS_AZURE_STORAGE_TEST_CONNECTION_STRING";
    private const string AzuriteImage = "mcr.microsoft.com/azure-storage/azurite:3.35.0";
    private readonly AzureStorageFixture _fixture;

    public AzureStoragePayloadStoreIntegrationTests(AzureStorageFixture fixture)
    {
        _fixture = fixture;
    }

    [AzureStorageFact]
    public async Task PayloadStore_WritesAndReadsPayload()
    {
        var store = await _fixture.CreateStoreAsync();
        var payload = BinaryData.FromString("""{"hello":"storage"}""");

        var reference = await store.WriteAsync(
            payload,
            new MiniBusPayloadWriteOptions
            {
                PayloadId = "payload-1",
                ContentType = "application/json"
            });

        var stored = await store.ReadAsync(reference);

        Assert.Equal(payload.ToString(), stored.ToString());
        Assert.Equal("application/json", reference.ContentType);
        Assert.Equal(payload.ToArray().LongLength, reference.Length);
        Assert.Equal("payload-1", reference.PayloadId);
    }

    [AzureStorageFact]
    public async Task PayloadStore_PreservesPayloadMetadata()
    {
        var createdUtc = new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);
        var store = await _fixture.CreateStoreAsync(options =>
        {
            options.UtcNowProvider = () => createdUtc;
            options.PayloadRetention = TimeSpan.FromHours(1);
        });

        var reference = await store.WriteAsync(
            BinaryData.FromString("metadata"),
            new MiniBusPayloadWriteOptions
            {
                PayloadId = "metadata-1",
                ContentType = "text/plain"
            });

        var blobClient = _fixture.CreateBlobClient(reference);
        var properties = await blobClient.GetPropertiesAsync();

        Assert.Equal(createdUtc, reference.CreatedUtc);
        Assert.Equal(createdUtc.AddHours(1), reference.ExpiresUtc);
        Assert.Equal("metadata-1", properties.Value.Metadata["minibus_payload_id"]);
        Assert.Equal(createdUtc.ToString("O"), properties.Value.Metadata["minibus_created_utc"]);
        Assert.Equal(createdUtc.AddHours(1).ToString("O"), properties.Value.Metadata["minibus_expires_utc"]);
    }

    [AzureStorageFact]
    public async Task PayloadStore_UsesDeterministicPayloadIdInBlobName()
    {
        var createdUtc = new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);
        var store = await _fixture.CreateStoreAsync(options =>
        {
            options.UtcNowProvider = () => createdUtc;
            options.BlobNamePrefix = "custom-prefix";
        });

        var reference = await store.WriteAsync(
            BinaryData.FromString("deterministic"),
            new MiniBusPayloadWriteOptions
            {
                PayloadId = "deterministic-1"
            });

        Assert.Equal("custom-prefix/2026/05/15/deterministic-1.bin", reference.BlobName);
    }

    [AzureStorageFact]
    public async Task PayloadStore_ReportsMissingPayloadClearly()
    {
        var store = await _fixture.CreateStoreAsync();
        var reference = new MiniBusPayloadReference(
            _fixture.ContainerName,
            "payloads/2026/05/15/missing.bin",
            "missing",
            Length: 0,
            ContentType: null,
            CreatedUtc: DateTimeOffset.UtcNow,
            ExpiresUtc: null);

        var exception = await Assert.ThrowsAsync<MiniBusPayloadNotFoundException>(
            () => store.ReadAsync(reference));

        Assert.Same(reference, exception.Reference);
    }

    [AzureStorageFact]
    public async Task PayloadStore_DeleteIsIdempotent()
    {
        var store = await _fixture.CreateStoreAsync();
        var reference = await store.WriteAsync(
            BinaryData.FromString("delete me"),
            new MiniBusPayloadWriteOptions
            {
                PayloadId = "delete-1"
            });

        await store.DeleteAsync(reference);
        await store.DeleteAsync(reference);

        await Assert.ThrowsAsync<MiniBusPayloadNotFoundException>(() => store.ReadAsync(reference));
    }

    [AzureStorageFact]
    public async Task PayloadStore_WritesNonSeekableStreams()
    {
        var store = await _fixture.CreateStoreAsync();
        await using var stream = new NonSeekableMemoryStream(Encoding.UTF8.GetBytes("streamed"));

        var reference = await store.WriteAsync(
            stream,
            new MiniBusPayloadWriteOptions
            {
                PayloadId = "streamed-1"
            });

        var stored = await store.ReadAsync(reference);

        Assert.Equal("streamed", stored.ToString());
        Assert.Equal(8, reference.Length);
    }

    [AzureStorageFact]
    public async Task AuditWriter_WritesAuditEnvelopeAndMetadata()
    {
        var auditedUtc = new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero);
        var writer = await _fixture.CreateAuditWriterAsync(options =>
        {
            options.UtcNowProvider = () => auditedUtc;
            options.AuditRetention = TimeSpan.FromHours(2);
            options.AuditBlobNamePrefix = "custom-audits";
        });
        var record = MiniBusAuditRecordTestData.Create(
            body: BinaryData.FromString("{}"),
            auditedUtc: auditedUtc,
            causationId: null);

        await writer.WriteAsync(record);

        var blobClient = _fixture.CreateAuditBlobClient(
            BlobMiniBusAuditWriter.CreateBlobName("custom-audits", auditedUtc, "audit-1"));
        var content = await blobClient.DownloadContentAsync();
        var properties = await blobClient.GetPropertiesAsync();

        Assert.Contains("\"messageId\":\"message-1\"", content.Value.Content.ToString(), StringComparison.Ordinal);
        Assert.Equal("audit-1", properties.Value.Metadata["minibus_audit_id"]);
        Assert.Equal("message-1", properties.Value.Metadata["minibus_message_id"]);
        Assert.Equal("Billing", properties.Value.Metadata["minibus_endpoint"]);
        Assert.Equal("Completed", properties.Value.Metadata["minibus_outcome"]);
        Assert.Equal(auditedUtc.AddHours(2).ToString("O"), properties.Value.Metadata["minibus_expires_utc"]);
    }

    public sealed class AzureStorageFixture : IAsyncLifetime
    {
        private readonly string? _externalConnectionString;
        private AzuriteContainer? _container;
        private Exception? _startupException;

        public AzureStorageFixture()
        {
            _externalConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
            ContainerName = $"minibus-payloads-{Guid.NewGuid():N}";
        }

        public string ContainerName { get; }

        public async Task InitializeAsync()
        {
            if (!string.IsNullOrWhiteSpace(_externalConnectionString))
            {
                return;
            }

            try
            {
                _container = new AzuriteBuilder(AzuriteImage).Build();
                await _container.StartAsync();
            }
            catch (Exception exception)
            {
                _startupException = exception;
            }
        }

        public async Task DisposeAsync()
        {
            if (_container is not null)
            {
                await _container.DisposeAsync();
            }
        }

        internal async Task<IMiniBusPayloadStore> CreateStoreAsync(
            Action<MiniBusAzureStoragePersistenceOptions>? configureOptions = null)
        {
            if (_startupException is not null)
            {
                throw new InvalidOperationException(
                    "Azurite Testcontainers startup failed. " +
                    $"Alternatively set {ConnectionStringEnvironmentVariable}. " +
                    $"Original error: {_startupException.Message}");
            }

            var connectionString = GetConnectionString();
            var options = new MiniBusAzureStoragePersistenceOptions
            {
                ConnectionString = connectionString,
                ContainerName = ContainerName,
                BlobContainerClientFactory = () => new BlobContainerClient(
                    connectionString,
                    ContainerName,
                    CreateBlobClientOptions())
            };
            configureOptions?.Invoke(options);
            MiniBusAzureStoragePersistenceOptionsValidator.Validate(options);

            var store = new BlobMiniBusPayloadStore(options);
            var containerClient = new BlobContainerClient(connectionString, ContainerName, CreateBlobClientOptions());
            await containerClient.CreateIfNotExistsAsync();
            return store;
        }

        internal async Task<BlobMiniBusAuditWriter> CreateAuditWriterAsync(
            Action<MiniBusAzureStoragePersistenceOptions>? configureOptions = null)
        {
            if (_startupException is not null)
            {
                throw new InvalidOperationException(
                    "Azurite Testcontainers startup failed. " +
                    $"Alternatively set {ConnectionStringEnvironmentVariable}. " +
                    $"Original error: {_startupException.Message}");
            }

            var connectionString = GetConnectionString();
            var options = new MiniBusAzureStoragePersistenceOptions
            {
                ConnectionString = connectionString,
                AuditContainerName = ContainerName,
                AuditBlobContainerClientFactory = () => new BlobContainerClient(
                    connectionString,
                    ContainerName,
                    CreateBlobClientOptions())
            };
            configureOptions?.Invoke(options);
            MiniBusAzureStoragePersistenceOptionsValidator.ValidateAudit(options);

            var containerClient = new BlobContainerClient(connectionString, ContainerName, CreateBlobClientOptions());
            await containerClient.CreateIfNotExistsAsync();
            return new BlobMiniBusAuditWriter(options);
        }

        internal BlobClient CreateBlobClient(MiniBusPayloadReference reference)
        {
            return new BlobContainerClient(GetConnectionString(), reference.ContainerName, CreateBlobClientOptions())
                .GetBlobClient(reference.BlobName);
        }

        internal BlobClient CreateAuditBlobClient(string blobName)
        {
            return new BlobContainerClient(GetConnectionString(), ContainerName, CreateBlobClientOptions())
                .GetBlobClient(blobName);
        }

        private static BlobClientOptions CreateBlobClientOptions()
        {
            return new BlobClientOptions(BlobClientOptions.ServiceVersion.V2024_11_04);
        }

        private string GetConnectionString()
        {
            var connectionString = !string.IsNullOrWhiteSpace(_externalConnectionString)
                ? _externalConnectionString
                : _container?.GetConnectionString();

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Set {ConnectionStringEnvironmentVariable} or enable Docker to run Azure Storage-backed MiniBus persistence tests.");
            }

            return connectionString;
        }
    }

    private sealed class AzureStorageFactAttribute : FactAttribute
    {
        public AzureStorageFactAttribute()
        {
            Timeout = 120_000;

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable))
                && !DockerSocketIsReachable())
            {
                Skip = "Docker is not reachable, and MINIBUS_AZURE_STORAGE_TEST_CONNECTION_STRING is not set. " +
                       "Start Docker Desktop or configure an external Azure Storage test connection string.";
            }
        }

        private static bool DockerSocketIsReachable()
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST")))
            {
                return true;
            }

            return UnixSocketIsReachable("/var/run/docker.sock")
                   || UnixSocketIsReachable(Path.Combine(
                       Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                       ".docker",
                       "run",
                       "docker.sock"));
        }

        private static bool UnixSocketIsReachable(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                var connectTask = socket.ConnectAsync(new UnixDomainSocketEndPoint(path));
                return connectTask.Wait(TimeSpan.FromMilliseconds(250)) && socket.Connected;
            }
            catch
            {
                return false;
            }
        }
    }

    private sealed class NonSeekableMemoryStream : MemoryStream
    {
        public NonSeekableMemoryStream(byte[] buffer)
            : base(buffer)
        {
        }

        public override bool CanSeek => false;
    }

}
