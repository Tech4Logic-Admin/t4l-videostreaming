namespace T4L.VideoSearch.Api.Domain.Entities;

/// <summary>
/// Tracks the processing pipeline for a video
/// </summary>
public class VideoProcessingJob
{
    public Guid Id { get; set; }
    public Guid VideoId { get; set; }
    public ProcessingStage Stage { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public int Attempts { get; set; }
    public int Progress { get; set; } // 0-100 percentage
    public string? ProgressMessage { get; set; }
    public string? LastError { get; set; }
    public string? ExternalJobId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public VideoAsset Video { get; set; } = null!;
}

public enum ProcessingStage
{
    MalwareScan,
    ContentModeration,
    Transcription,
    SearchIndexing,
    ThumbnailGeneration,
    Encoding,
    AIHighlights
}

public enum JobStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Skipped
}
