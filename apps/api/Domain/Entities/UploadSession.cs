namespace T4L.VideoSearch.Api.Domain.Entities;

/// <summary>
/// Tracks a chunked upload session
/// </summary>
public class UploadSession
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public long ChunkSize { get; set; }
    public int TotalChunks { get; set; }
    public int UploadedChunks { get; set; }
    public UploadSessionStatus Status { get; set; } = UploadSessionStatus.Created;
    public string BlobPath { get; set; } = string.Empty;
    public string CreatedByOid { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    // Video metadata (provided at session creation)
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string[] Tags { get; set; } = [];
    public string? LanguageHint { get; set; }

    // Block IDs for Azure Blob block commit
    public string[] BlockIds { get; set; } = [];

    // Navigation
    public Guid? VideoAssetId { get; set; }
    public VideoAsset? VideoAsset { get; set; }
}

public enum UploadSessionStatus
{
    Created,
    Uploading,
    Completing,
    Completed,
    Failed,
    Expired
}
