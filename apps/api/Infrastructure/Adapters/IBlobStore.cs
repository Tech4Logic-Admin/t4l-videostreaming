namespace T4L.VideoSearch.Api.Infrastructure.Adapters;

/// <summary>
/// Abstraction for blob storage operations
/// </summary>
public interface IBlobStore
{
    /// <summary>
    /// Generate a signed URL for uploading a blob
    /// </summary>
    Task<BlobUploadUrl> GenerateUploadUrlAsync(string containerName, string blobName, TimeSpan expiry, CancellationToken ct = default);

    /// <summary>
    /// Generate a signed URL for reading a blob
    /// </summary>
    Task<string> GenerateReadUrlAsync(string containerName, string blobName, TimeSpan expiry, CancellationToken ct = default);

    /// <summary>
    /// Move a blob from one container to another
    /// </summary>
    Task MoveBlobAsync(string sourceContainer, string sourceBlobName, string targetContainer, string targetBlobName, CancellationToken ct = default);

    /// <summary>
    /// Delete a blob
    /// </summary>
    Task DeleteBlobAsync(string containerName, string blobName, CancellationToken ct = default);

    /// <summary>
    /// Check if a blob exists
    /// </summary>
    Task<bool> BlobExistsAsync(string containerName, string blobName, CancellationToken ct = default);

    /// <summary>
    /// Get blob metadata
    /// </summary>
    Task<BlobMetadata?> GetBlobMetadataAsync(string containerName, string blobName, CancellationToken ct = default);
}

public record BlobUploadUrl(string Url, string BlobPath, DateTime ExpiresAt);

public record BlobMetadata(string BlobName, long SizeBytes, string ContentType, DateTime CreatedAt);
