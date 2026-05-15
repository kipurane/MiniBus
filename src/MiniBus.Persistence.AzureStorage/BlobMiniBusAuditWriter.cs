using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using MiniBus.Core.Auditing;

namespace MiniBus.Persistence.AzureStorage;

public sealed class BlobMiniBusAuditWriter : IMiniBusAuditWriter
{
    private const string AuditIdMetadataName = "minibus_audit_id";
    private const string MessageIdMetadataName = "minibus_message_id";
    private const string EndpointNameMetadataName = "minibus_endpoint";
    private const string MessageTypeMetadataName = "minibus_message_type";
    private const string OutcomeMetadataName = "minibus_outcome";
    private const string AuditedUtcMetadataName = "minibus_audited_utc";
    private const string ExpiresUtcMetadataName = "minibus_expires_utc";
    // Azure Blob user metadata values are stored as HTTP headers and have tight size limits.
    // Keep index metadata compact; the full audit envelope remains in the blob content.
    private const int MaxMetadataValueLength = 512;

    private readonly MiniBusAzureStoragePersistenceOptions _options;
    private readonly BlobContainerClient _containerClient;

    public BlobMiniBusAuditWriter(MiniBusAzureStoragePersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        MiniBusAzureStoragePersistenceOptionsValidator.ValidateAudit(options);
        _options = options;
        _containerClient = options.AuditBlobContainerClientFactory!();
    }

    public async Task WriteAsync(
        MiniBusAuditRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        ValidateAuditId(record.AuditId);

        var expiresUtc = GetExpiresUtc(record.AuditedUtc);
        var blobName = CreateBlobName(_options.AuditBlobNamePrefix, record.AuditedUtc, record.AuditId);
        var blobClient = _containerClient.GetBlobClient(blobName);
        var content = MiniBusAuditEnvelopeJsonSerializer.Serialize(record);
        var metadata = CreateMetadata(record, expiresUtc);

        await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await blobClient.UploadAsync(
                content.ToStream(),
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = "application/json"
                    },
                    Metadata = metadata
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public static string CreateBlobName(
        string? prefix,
        DateTimeOffset auditedUtc,
        string auditId)
    {
        ValidateAuditId(auditId);

        var normalizedPrefix = NormalizePrefix(prefix);
        var partition = auditedUtc.UtcDateTime.ToString("yyyy'/'MM'/'dd");
        var fileName = $"{auditId}.json";

        return string.IsNullOrWhiteSpace(normalizedPrefix)
            ? $"{partition}/{fileName}"
            : $"{normalizedPrefix}/{partition}/{fileName}";
    }

    public static void ValidateAuditId(string auditId)
    {
        if (string.IsNullOrWhiteSpace(auditId))
        {
            throw new ArgumentException("MiniBus audit id cannot be empty.", nameof(auditId));
        }

        if (auditId.Length > 200)
        {
            throw new ArgumentException("MiniBus audit id cannot be longer than 200 characters.", nameof(auditId));
        }

        if (auditId is "." or ".."
            || auditId.Contains('/', StringComparison.Ordinal)
            || auditId.Contains('\\', StringComparison.Ordinal)
            || auditId.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)))
        {
            throw new ArgumentException(
                "MiniBus audit id can contain visible non-whitespace characters only and cannot include path separators.",
                nameof(auditId));
        }
    }

    private DateTimeOffset? GetExpiresUtc(DateTimeOffset auditedUtc)
    {
        return _options.AuditRetention is null
            ? null
            : auditedUtc.Add(_options.AuditRetention.Value);
    }

    private static Dictionary<string, string> CreateMetadata(
        MiniBusAuditRecord record,
        DateTimeOffset? expiresUtc)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AuditIdMetadataName] = ToMetadataValue(record.AuditId),
            [MessageIdMetadataName] = ToMetadataValue(record.MessageId),
            [EndpointNameMetadataName] = ToMetadataValue(record.EndpointName),
            [OutcomeMetadataName] = record.Outcome.ToString(),
            [AuditedUtcMetadataName] = record.AuditedUtc.ToString("O")
        };

        if (!string.IsNullOrWhiteSpace(record.MessageType))
        {
            metadata[MessageTypeMetadataName] = ToMetadataValue(record.MessageType);
        }

        if (expiresUtc is not null)
        {
            metadata[ExpiresUtcMetadataName] = expiresUtc.Value.ToString("O");
        }

        return metadata;
    }

    private static string ToMetadataValue(string value)
    {
        return value.Length <= MaxMetadataValueLength
            ? value
            : value[..MaxMetadataValueLength];
    }

    private static string NormalizePrefix(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return string.Empty;
        }

        var normalizedPrefix = prefix.Trim().Trim('/');
        if (normalizedPrefix.Contains('\\', StringComparison.Ordinal)
            || normalizedPrefix.Split('/').Any(segment => segment is "." or ".."))
        {
            throw new InvalidOperationException(
                $"MiniBus audit blob prefix '{prefix}' is not safe.");
        }

        return normalizedPrefix;
    }
}
