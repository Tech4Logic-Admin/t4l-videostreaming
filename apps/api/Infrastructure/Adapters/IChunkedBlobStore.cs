namespace T4L.VideoSearch.Api.Infrastructure.Adapters;

/// <summary>
/// Extended blob storage operations for chunked uploads
/// </summary>
public interface IChunkedBlobStore : IBlobStore
{
    /// <summary>
    /// Stage a block for a blob (part of chunked upload)
    /// </summary>
    Task StageBlockAsync(string containerName, string blobName, string blockId, Stream content, CancellationToken ct = default);

    /// <summary>
    /// Commit all staged blocks to finalize the blob
    /// </summary>
    Task CommitBlocksAsync(string containerName, string blobName, IEnumerable<string> blockIds, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Get list of uncommitted blocks for a blob
    /// </summary>
    Task<IReadOnlyList<string>> GetUncommittedBlocksAsync(string containerName, string blobName, CancellationToken ct = default);

    /// <summary>
    /// Generate a SAS URL for direct chunk upload (browser upload)
    /// </summary>
    Task<ChunkUploadUrl> GenerateChunkUploadUrlAsync(string containerName, string blobName, string blockId, TimeSpan expiry, CancellationToken ct = default);
}

public record ChunkUploadUrl(
    string Url,
    string BlockId,
    DateTime ExpiresAt,
    Dictionary<string, string> RequiredHeaders
);
