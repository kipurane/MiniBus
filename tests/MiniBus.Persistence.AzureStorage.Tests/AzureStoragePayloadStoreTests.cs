using Azure.Storage.Blobs;
using MiniBus.Core.Auditing;
using MiniBus.Core.ClaimCheck;
using Microsoft.Extensions.DependencyInjection;
using MiniBus.Persistence.AzureStorage.DependencyInjection;
using Xunit;

namespace MiniBus.Persistence.AzureStorage.Tests;

public sealed class AzureStoragePayloadStoreTests
{
    [Fact]
    public void Validate_RejectsMissingBlobClientFactory()
    {
        var options = new MiniBusAzureStoragePersistenceOptions();

        var exception = Assert.Throws<InvalidOperationException>(
            () => MiniBusAzureStoragePersistenceOptionsValidator.Validate(options));

        Assert.Contains("connection string or BlobContainerClient factory", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsInvalidContainerName()
    {
        var options = new MiniBusAzureStoragePersistenceOptions
        {
            ContainerName = "invalid container",
            BlobContainerClientFactory = () => new BlobContainerClient("UseDevelopmentStorage=true", "invalid")
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => MiniBusAzureStoragePersistenceOptionsValidator.Validate(options));

        Assert.Contains("container name", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsInvalidPayloadRetention()
    {
        var options = new MiniBusAzureStoragePersistenceOptions
        {
            PayloadRetention = TimeSpan.Zero,
            BlobContainerClientFactory = () => new BlobContainerClient("UseDevelopmentStorage=true", "minibus-payloads")
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => MiniBusAzureStoragePersistenceOptionsValidator.Validate(options));

        Assert.Contains("payload retention", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddMiniBusAzureStoragePersistence_RegistersPayloadStore()
    {
        var services = new ServiceCollection();

        services.AddMiniBusAzureStoragePersistence(options =>
        {
            options.ContainerName = "minibus-payloads";
            options.BlobContainerClientFactory = () => new BlobContainerClient("UseDevelopmentStorage=true", "minibus-payloads");
        });

        using var provider = services.BuildServiceProvider();

        Assert.IsType<BlobMiniBusPayloadStore>(provider.GetRequiredService<IMiniBusPayloadStore>());
        Assert.Same(
            provider.GetRequiredService<IMiniBusPayloadStore>(),
            provider.GetRequiredService<IMiniBusClaimCheckPayloadStore>());
    }

    [Fact]
    public void AddMiniBusAzureStoragePersistence_InvokesBlobClientFactoryOnceForSingletonStore()
    {
        var services = new ServiceCollection();
        var invocationCount = 0;

        services.AddMiniBusAzureStoragePersistence(options =>
        {
            options.ContainerName = "minibus-payloads";
            options.BlobContainerClientFactory = () =>
            {
                invocationCount++;
                return new BlobContainerClient("UseDevelopmentStorage=true", "minibus-payloads");
            };
        });

        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IMiniBusPayloadStore>();
        var second = provider.GetRequiredService<IMiniBusPayloadStore>();

        Assert.Same(first, second);
        Assert.Equal(1, invocationCount);
    }

    [Fact]
    public void AddMiniBusAzureBlobClaimCheck_RegistersEnabledOptions()
    {
        var services = new ServiceCollection();

        services.AddMiniBusAzureBlobClaimCheck(1024);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<MiniBusClaimCheckOptions>();

        Assert.True(options.Enabled);
        Assert.Equal(1024, options.PayloadThresholdBytes);
        Assert.Equal(MiniBusClaimCheckProviderNames.AzureBlobStorage, options.Provider);
    }

    [Fact]
    public void AddMiniBusAzureStoragePersistence_AppliesConnectionStringFactory()
    {
        var services = new ServiceCollection();

        services.AddMiniBusAzureStoragePersistence("UseDevelopmentStorage=true", "minibus-payloads");

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<MiniBusAzureStoragePersistenceOptions>();

        Assert.NotNull(options.BlobContainerClientFactory);
    }

    [Fact]
    public void ValidateAudit_RejectsMissingAuditBlobClientFactory()
    {
        var options = new MiniBusAzureStoragePersistenceOptions();

        var exception = Assert.Throws<InvalidOperationException>(
            () => MiniBusAzureStoragePersistenceOptionsValidator.ValidateAudit(options));

        Assert.Contains("audit writing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateAudit_RejectsInvalidAuditRetention()
    {
        var options = new MiniBusAzureStoragePersistenceOptions
        {
            AuditRetention = TimeSpan.Zero,
            AuditBlobContainerClientFactory = () => new BlobContainerClient("UseDevelopmentStorage=true", "minibus-audits")
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => MiniBusAzureStoragePersistenceOptionsValidator.ValidateAudit(options));

        Assert.Contains("audit retention", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddMiniBusAzureBlobAudit_RegistersAuditWriter()
    {
        var services = new ServiceCollection();

        services.AddMiniBusAzureBlobAudit(options =>
        {
            options.AuditContainerName = "minibus-audits";
            options.AuditBlobContainerClientFactory = () => new BlobContainerClient("UseDevelopmentStorage=true", "minibus-audits");
        });

        using var provider = services.BuildServiceProvider();

        Assert.IsType<BlobMiniBusAuditWriter>(provider.GetRequiredService<IMiniBusAuditWriter>());
    }

    [Fact]
    public void AddMiniBusAzureBlobAudit_InvokesBlobClientFactoryOnceForSingletonWriter()
    {
        var services = new ServiceCollection();
        var invocationCount = 0;

        services.AddMiniBusAzureBlobAudit(options =>
        {
            options.AuditContainerName = "minibus-audits";
            options.AuditBlobContainerClientFactory = () =>
            {
                invocationCount++;
                return new BlobContainerClient("UseDevelopmentStorage=true", "minibus-audits");
            };
        });

        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IMiniBusAuditWriter>();
        var second = provider.GetRequiredService<IMiniBusAuditWriter>();

        Assert.Same(first, second);
        Assert.Equal(1, invocationCount);
    }

    [Fact]
    public void AddMiniBusAzureBlobAudit_AppliesConnectionStringFactory()
    {
        var services = new ServiceCollection();

        services.AddMiniBusAzureBlobAudit("UseDevelopmentStorage=true", "minibus-audits");

        using var provider = services.BuildServiceProvider();

        Assert.IsType<BlobMiniBusAuditWriter>(provider.GetRequiredService<IMiniBusAuditWriter>());
    }

    [Fact]
    public void CreateBlobName_UsesConfiguredPrefixDatePartitionAndPayloadId()
    {
        var blobName = BlobMiniBusPayloadStore.CreateBlobName(
            "payloads",
            new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero),
            "payload-1");

        Assert.Equal("payloads/2026/05/15/payload-1.bin", blobName);
    }

    [Fact]
    public void CreateAuditBlobName_UsesConfiguredPrefixDatePartitionAndAuditId()
    {
        var blobName = BlobMiniBusAuditWriter.CreateBlobName(
            "audits",
            new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero),
            "audit-1");

        Assert.Equal("audits/2026/05/15/audit-1.json", blobName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("folder/payload")]
    [InlineData("folder\\payload")]
    [InlineData("payload id")]
    [InlineData(".")]
    [InlineData("..")]
    public void ValidatePayloadId_RejectsUnsafePayloadIds(string payloadId)
    {
        Assert.Throws<ArgumentException>(() => BlobMiniBusPayloadStore.ValidatePayloadId(payloadId));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("folder/audit")]
    [InlineData("folder\\audit")]
    [InlineData("audit id")]
    [InlineData(".")]
    [InlineData("..")]
    public void ValidateAuditId_RejectsUnsafeAuditIds(string auditId)
    {
        Assert.Throws<ArgumentException>(() => BlobMiniBusAuditWriter.ValidateAuditId(auditId));
    }

    [Fact]
    public void AuditRecord_DoesNotExposeAzureSdkTypes()
    {
        var record = MiniBusAuditRecordTestData.Create();

        var azureProperty = record
            .GetType()
            .GetProperties()
            .FirstOrDefault(property => property.PropertyType.Namespace?.StartsWith("Azure", StringComparison.Ordinal) == true);

        Assert.Null(azureProperty);
    }

    [Fact]
    public void AuditEnvelopeSerializer_IncludesMetadataAndBase64Body()
    {
        var record = MiniBusAuditRecordTestData.Create(body: BinaryData.FromString("hello"));

        var json = MiniBusAuditEnvelopeJsonSerializer.Serialize(record).ToString();

        Assert.Contains("\"messageId\":\"message-1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"outcome\":\"Completed\"", json, StringComparison.Ordinal);
        Assert.Contains("\"bodyBase64\":\"aGVsbG8=\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void PayloadReference_DoesNotExposeAzureSdkTypes()
    {
        var reference = new MiniBusPayloadReference(
            "minibus-payloads",
            "payloads/2026/05/15/payload-1.bin",
            "payload-1",
            Length: 12,
            ContentType: "application/json",
            CreatedUtc: DateTimeOffset.UtcNow,
            ExpiresUtc: null);

        var azureProperty = reference
            .GetType()
            .GetProperties()
            .FirstOrDefault(property => property.PropertyType.Namespace?.StartsWith("Azure", StringComparison.Ordinal) == true);

        Assert.Null(azureProperty);
    }

}
