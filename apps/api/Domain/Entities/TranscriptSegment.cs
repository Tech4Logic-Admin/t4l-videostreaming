namespace T4L.VideoSearch.Api.Domain.Entities;

/// <summary>
/// Represents a segment of transcribed text with timestamps
/// </summary>
public class TranscriptSegment
{
    public Guid Id { get; set; }
    public Guid VideoId { get; set; }
    public long StartMs { get; set; }
    public long EndMs { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? DetectedLanguage { get; set; }
    public string? Speaker { get; set; }
    public float[]? EmbeddingVector { get; set; }
    public float? Confidence { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public VideoAsset Video { get; set; } = null!;
}
