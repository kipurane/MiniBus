using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace MiniBus.Persistence.AzureStorage;

public sealed class BlobMiniBusPayloadStore : IMiniBusPayloadStore
{
    private const string PayloadIdMetadataName = "minibus_payload_id";
    private const string CreatedUtcMetadataName = "minibus_created_utc";
    private const string ExpiresUtcMetadataName = "minibus_expires_utc";
    private const string LengthMetadataName = "minibus_length";

    private readonly MiniBusAzureStoragePersistenceOptions _options;
    private readonly BlobContainerClient _containerClient;

    public BlobMiniBusPayloadStore(MiniBusAzureStoragePersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        MiniBusAzureStoragePersistenceOptionsValidator.Validate(options);
        _options = options;
        _containerClient = options.BlobContainerClientFactory!();
    }

    public Task<MiniBusPayloadReference> WriteAsync(
        BinaryData payload,
        MiniBusPayloadWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return WriteAsync(payload.ToStream(), options, cancellationToken);
    }

    public async Task<MiniBusPayloadReference> WriteAsync(
        Stream payload,
        MiniBusPayloadWriteOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var preparedPayload = await PreparePayloadAsync(payload, cancellationToken).ConfigureAwait(false);
        var createdUtc = _options.UtcNowProvider();
        var payloadId = GetPayloadId(options);
        var blobName = CreateBlobName(_options.BlobNamePrefix, createdUtc, payloadId);
        var expiresUtc = options?.ExpiresUtc ?? GetExpiresUtc(createdUtc);
        var blobClient = _containerClient.GetBlobClient(blobName);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [PayloadIdMetadataName] = payloadId,
            [CreatedUtcMetadataName] = createdUtc.ToString("O"),
            [LengthMetadataName] = preparedPayload.Length.ToString()
        };

        if (expiresUtc is not null)
        {
            metadata[ExpiresUtcMetadataName] = expiresUtc.Value.ToString("O");
        }

        await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await blobClient.UploadAsync(
                preparedPayload.Stream,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = options?.ContentType
                    },
                    Metadata = metadata
                },
                cancellationToken)
            .ConfigureAwait(false);

        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        return new MiniBusPayloadReference(
            _containerClient.Name,
            blobName,
            payloadId,
            properties.Value.ContentLength,
            properties.Value.ContentType,
            createdUtc,
            expiresUtc);
    }

    public async Task<Stream> OpenReadAsync(
        MiniBusPayloadReference reference,
        CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(reference);

        try
        {
            var response = await blobClient.OpenReadAsync(
                    new BlobOpenReadOptions(allowModifications: false),
                    cancellationToken)
                .ConfigureAwait(false);
            return response;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            throw new MiniBusPayloadNotFoundException(reference);
        }
    }

    public async Task<BinaryData> ReadAsync(
        MiniBusPayloadReference reference,
        CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(reference);

        try
        {
            var result = await blobClient.DownloadContentAsync(cancellationToken).ConfigureAwait(false);
            return result.Value.Content;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            throw new MiniBusPayloadNotFoundException(reference);
        }
    }

    public async Task DeleteAsync(
        MiniBusPayloadReference reference,
        CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(reference);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public static string CreateBlobName(
        string? prefix,
        DateTimeOffset createdUtc,
        string payloadId)
    {
        ValidatePayloadId(payloadId);

        var normalizedPrefix = NormalizePrefix(prefix);
        var partition = createdUtc.UtcDateTime.ToString("yyyy'/'MM'/'dd");
        var fileName = $"{payloadId}.bin";

        return string.IsNullOrWhiteSpace(normalizedPrefix)
            ? $"{partition}/{fileName}"
            : $"{normalizedPrefix}/{partition}/{fileName}";
    }

    public static void ValidatePayloadId(string payloadId)
    {
        if (string.IsNullOrWhiteSpace(payloadId))
        {
            throw new ArgumentException("MiniBus payload id cannot be empty.", nameof(payloadId));
        }

        if (payloadId.Length > 200)
        {
            throw new ArgumentException("MiniBus payload id cannot be longer than 200 characters.", nameof(payloadId));
        }

        if (payloadId is "." or ".."
            || payloadId.Contains('/', StringComparison.Ordinal)
            || payloadId.Contains('\\', StringComparison.Ordinal)
            || payloadId.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
        {
            throw new ArgumentException(
                "MiniBus payload id can contain visible non-whitespace characters only and cannot include path separators.",
                nameof(payloadId));
        }
    }

    private BlobClient GetBlobClient(MiniBusPayloadReference reference)
    {
        ValidateReference(reference, _containerClient.Name);
        return _containerClient.GetBlobClient(reference.BlobName);
    }

    private static void ValidateReference(
        MiniBusPayloadReference reference,
        string expectedContainerName)
    {
        ArgumentNullException.ThrowIfNull(reference);

        if (!string.Equals(reference.ContainerName, expectedContainerName, StringComparison.Ordinal))
        {
            throw new MiniBusInvalidPayloadReferenceException(
                $"MiniBus payload reference targets container '{reference.ContainerName}', but the configured container is '{expectedContainerName}'.");
        }

        if (string.IsNullOrWhiteSpace(reference.BlobName))
        {
            throw new MiniBusInvalidPayloadReferenceException("MiniBus payload reference blob name cannot be empty.");
        }

        if (reference.BlobName.Contains('\\', StringComparison.Ordinal)
            || reference.BlobName.Split('/').Any(segment => segment is "." or ".."))
        {
            throw new MiniBusInvalidPayloadReferenceException(
                $"MiniBus payload reference blob name '{reference.BlobName}' is not safe.");
        }

        ValidatePayloadId(reference.PayloadId);
    }

    private string GetPayloadId(MiniBusPayloadWriteOptions? options)
    {
        var payloadId = options?.PayloadId ?? _options.PayloadIdFactory();
        ValidatePayloadId(payloadId);
        return payloadId;
    }

    private DateTimeOffset? GetExpiresUtc(DateTimeOffset createdUtc)
    {
        return _options.PayloadRetention is null
            ? null
            : createdUtc.Add(_options.PayloadRetention.Value);
    }

    private static async Task<PreparedPayload> PreparePayloadAsync(
        Stream payload,
        CancellationToken cancellationToken)
    {
        if (payload.CanSeek)
        {
            return new PreparedPayload(payload, payload.Length - payload.Position);
        }

        var bufferedPayload = new MemoryStream();
        await payload.CopyToAsync(bufferedPayload, cancellationToken).ConfigureAwait(false);
        bufferedPayload.Position = 0;
        return new PreparedPayload(bufferedPayload, bufferedPayload.Length);
    }

    private static string NormalizePrefix(string? prefix)
    {
        return string.IsNullOrWhiteSpace(prefix)
            ? string.Empty
            : prefix.Trim().Trim('/');
    }

    private sealed record PreparedPayload(Stream Stream, long Length);
}
