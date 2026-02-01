namespace T4L.VideoSearch.Api.Domain.Entities;

/// <summary>
/// Represents a video asset uploaded to the system
/// </summary>
public class VideoAsset
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string[] Tags { get; set; } = [];
    public string? LanguageHint { get; set; }
    public long? DurationMs { get; set; }
    public VideoStatus Status { get; set; } = VideoStatus.Uploading;
    public string BlobPath { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; }
    public string? MasterPlaylistPath { get; set; }
    public string CreatedByOid { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ACL
    public string[] AllowedGroupIds { get; set; } = [];
    public string[] AllowedUserOids { get; set; } = [];

    // Navigation
    public ICollection<VideoProcessingJob> ProcessingJobs { get; set; } = [];
    public ICollection<TranscriptSegment> TranscriptSegments { get; set; } = [];
    public ICollection<VideoVariant> Variants { get; set; } = [];
    public ICollection<VideoHighlight> Highlights { get; set; } = [];
    public VideoSummary? Summary { get; set; }
    public ModerationResult? ModerationResult { get; set; }
}

public enum VideoStatus
{
    Uploading,
    Queued,
    Processing,
    Scanning,
    Moderating,
    Indexing,
    Published,
    Quarantined,
    Rejected,
    Failed
}
