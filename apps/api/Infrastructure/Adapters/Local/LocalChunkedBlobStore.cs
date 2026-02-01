using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;

namespace T4L.VideoSearch.Api.Infrastructure.Adapters.Local;

/// <summary>
/// Local implementation of chunked blob store using Azurite
/// Supports block blob operations for chunked uploads
/// </summary>
public class LocalChunkedBlobStore : LocalBlobStore, IChunkedBlobStore
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<LocalChunkedBlobStore> _logger;

    public LocalChunkedBlobStore(
        BlobServiceClient blobServiceClient,
        IOptions<BlobStorageOptions> options,
        ILogger<LocalChunkedBlobStore> logger)
        : base(blobServiceClient, options, logger)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task StageBlockAsync(string containerName, string blobName, string blockId, Stream content, CancellationToken ct = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: ct);

        var blockBlobClient = containerClient.GetBlockBlobClient(blobName);

        // Stage the block
        await blockBlobClient.StageBlockAsync(blockId, content, cancellationToken: ct);

        _logger.LogDebug("Staged block {BlockId} for {Container}/{Blob}", blockId, containerName, blobName);
    }

    public async Task CommitBlocksAsync(string containerName, string blobName, IEnumerable<string> blockIds, string contentType, CancellationToken ct = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blockBlobClient = containerClient.GetBlockBlobClient(blobName);

        var httpHeaders = new BlobHttpHeaders
        {
            ContentType = contentType
        };

        // Commit all blocks in order
        await blockBlobClient.CommitBlockListAsync(
            blockIds,
            new CommitBlockListOptions { HttpHeaders = httpHeaders },
            cancellationToken: ct);

        _logger.LogInformation("Committed {BlockCount} blocks to {Container}/{Blob}",
            blockIds.Count(), containerName, blobName);
    }

    public async Task<IReadOnlyList<string>> GetUncommittedBlocksAsync(string containerName, string blobName, CancellationToken ct = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        var blockBlobClient = containerClient.GetBlockBlobClient(blobName);

        try
        {
            var blockList = await blockBlobClient.GetBlockListAsync(BlockListTypes.Uncommitted, cancellationToken: ct);
            return blockList.Value.UncommittedBlocks.Select(b => b.Name).ToList();
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            // Blob doesn't exist yet, return empty list
            return [];
        }
    }

    public async Task<ChunkUploadUrl> GenerateChunkUploadUrlAsync(string containerName, string blobName, string blockId, TimeSpan expiry, CancellationToken ct = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: ct);

        var blockBlobClient = containerClient.GetBlockBlobClient(blobName);

        // Generate SAS for the block blob with write permission
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerName,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiry)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

        var sasUri = blockBlobClient.GenerateSasUri(sasBuilder);

        // Build the URL for PUT Block operation
        var blockUploadUrl = $"{sasUri}&comp=block&blockid={Uri.EscapeDataString(blockId)}";

        _logger.LogDebug("Generated chunk upload URL for block {BlockId} of {Container}/{Blob}",
            blockId, containerName, blobName);

        return new ChunkUploadUrl(
            blockUploadUrl,
            blockId,
            DateTime.UtcNow.Add(expiry),
            new Dictionary<string, string>
            {
                ["x-ms-blob-type"] = "BlockBlob"
            }
        );
    }
}
