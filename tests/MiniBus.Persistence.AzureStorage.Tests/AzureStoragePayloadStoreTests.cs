using Azure.Storage.Blobs;
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
    public void CreateBlobName_UsesConfiguredPrefixDatePartitionAndPayloadId()
    {
        var blobName = BlobMiniBusPayloadStore.CreateBlobName(
            "payloads",
            new DateTimeOffset(2026, 5, 15, 12, 0, 0, TimeSpan.Zero),
            "payload-1");

        Assert.Equal("payloads/2026/05/15/payload-1.bin", blobName);
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
