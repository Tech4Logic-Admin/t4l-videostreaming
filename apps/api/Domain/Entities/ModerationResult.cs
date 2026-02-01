namespace T4L.VideoSearch.Api.Domain.Entities;

/// <summary>
/// Stores moderation results for a video
/// </summary>
public class ModerationResult
{
    public Guid Id { get; set; }
    public Guid VideoId { get; set; }
    public MalwareScanStatus MalwareScanStatus { get; set; } = MalwareScanStatus.Pending;
    public ContentSafetyStatus ContentSafetyStatus { get; set; } = ContentSafetyStatus.Pending;
    public string[] Reasons { get; set; } = [];
    public ModerationSeverity? HighestSeverity { get; set; }
    public ReviewerDecision? ReviewerDecision { get; set; }
    public string? ReviewerOid { get; set; }
    public string? ReviewerNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }

    // Navigation
    public VideoAsset Video { get; set; } = null!;
}

public enum MalwareScanStatus
{
    Pending,
    Clean,
    Infected,
    Error
}

public enum ContentSafetyStatus
{
    Pending,
    Safe,
    Flagged,
    Error
}

public enum ModerationSeverity
{
    Low,
    Medium,
    High
}

public enum ReviewerDecision
{
    Approved,
    Rejected
}
