using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace T4L.VideoSearch.Api.Infrastructure.Adapters.Local;

/// <summary>
/// Local blob store implementation using Azurite
/// </summary>
public class LocalBlobStore : IBlobStore
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<LocalBlobStore> _logger;
    private readonly BlobStorageOptions _options;

    public LocalBlobStore(
        BlobServiceClient blobServiceClient,
        IOptions<BlobStorageOptions> options,
        ILogger<LocalBlobStore> logger)
    {
        _blobServiceClient = blobServiceClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<BlobUploadUrl> GenerateUploadUrlAsync(string containerName, string blobName, TimeSpan expiry, CancellationToken ct = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: ct);

        var blobClient = containerClient.GetBlobClient(blobName);

        // For Azurite, we use account-level SAS
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiry)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

        var sasUri = blobClient.GenerateSasUri(sasBuilder);

        _logger.LogDebug("Generated upload URL for {Container}/{Blob}", containerName, blobName);

        return new BlobUploadUrl(
            sasUri.ToString(),
            $"{containerName}/{blobName}",
            DateTime.UtcNow.Add(expiry)
        );
    }

    public async Task<string> GenerateReadUrlAsync(string containerName, string blobName, TimeSpan expiry, CancellationToken ct = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync(ct))
        {
            throw new FileNotFoundException($"Blob {containerName}/{blobName} not found");
        }

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiry)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasUri = blobClient.GenerateSasUri(sasBuilder);

        _logger.LogDebug("Generated read URL for {Container}/{Blob}", containerName, blobName);

        return sasUri.ToString();
    }

    public async Task MoveBlobAsync(string sourceContainer, string sourceBlobName, string targetContainer, string targetBlobName, CancellationToken ct = default)
    {
        var sourceContainerClient = _blobServiceClient.GetBlobContainerClient(sourceContainer);
        var sourceBlobClient = sourceContainerClient.GetBlobClient(sourceBlobName);

        var targetContainerClient = _blobServiceClient.GetBlobContainerClient(targetContainer);
        await targetContainerClient.CreateIfNotExistsAsync(cancellationToken: ct);
        var targetBlobClient = targetContainerClient.GetBlobClient(targetBlobName);

        // Copy and then delete
        await targetBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri, cancellationToken: ct);
        await sourceBlobClient.DeleteIfExistsAsync(cancellationToken: ct);

        _logger.LogInformation("Moved blob from {Source} to {Target}",
            $"{sourceContainer}/{sourceBlobName}",
            $"{targetContainer}/{targetBlobName}");
    }

    public async Task DeleteBlobAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync(cancellationToken: ct);

        _logger.LogInformation("Deleted blob {Container}/{Blob}", containerName, blobName);
    }

    public async Task<bool> BlobExistsAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);
        return await blobClient.ExistsAsync(ct);
    }

    public async Task<BlobMetadata?> GetBlobMetadataAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync(ct))
        {
            return null;
        }

        var properties = await blobClient.GetPropertiesAsync(cancellationToken: ct);

        return new BlobMetadata(
            blobName,
            properties.Value.ContentLength,
            properties.Value.ContentType,
            properties.Value.CreatedOn.UtcDateTime
        );
    }
}

public class BlobStorageOptions
{
    public string ConnectionString { get; set; } = "UseDevelopmentStorage=true";
    public string QuarantineContainer { get; set; } = "quarantine";
    public string ApprovedContainer { get; set; } = "approved";
}
