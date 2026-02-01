namespace T4L.VideoSearch.Api.Domain.Entities;

/// <summary>
/// Represents a transcoded variant of a video for adaptive bitrate streaming
/// </summary>
public class VideoVariant
{
    public Guid Id { get; set; }
    public Guid VideoId { get; set; }

    /// <summary>
    /// Quality profile name (e.g., "1080p", "720p", "480p", "360p")
    /// </summary>
    public string Quality { get; set; } = string.Empty;

    /// <summary>
    /// Video width in pixels
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Video height in pixels
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Target video bitrate in kbps
    /// </summary>
    public int VideoBitrateKbps { get; set; }

    /// <summary>
    /// Target audio bitrate in kbps
    /// </summary>
    public int AudioBitrateKbps { get; set; }

    /// <summary>
    /// Path to the HLS playlist (.m3u8) for this variant
    /// </summary>
    public string PlaylistPath { get; set; } = string.Empty;

    /// <summary>
    /// Path to the segment files directory
    /// </summary>
    public string SegmentsPath { get; set; } = string.Empty;

    /// <summary>
    /// Total file size of all segments in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Encoding status
    /// </summary>
    public VariantStatus Status { get; set; } = VariantStatus.Pending;

    /// <summary>
    /// Encoding progress percentage (0-100)
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// Current progress message
    /// </summary>
    public string? ProgressMessage { get; set; }

    /// <summary>
    /// Error message if encoding failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public VideoAsset Video { get; set; } = null!;
}

public enum VariantStatus
{
    Pending,
    Encoding,
    Completed,
    Failed
}

/// <summary>
/// Predefined quality profiles for ABR streaming
/// </summary>
public static class QualityProfiles
{
    public static readonly QualityProfile[] All =
    [
        new("1080p", 1920, 1080, 5000, 192),
        new("720p", 1280, 720, 2500, 128),
        new("480p", 854, 480, 1000, 96),
        new("360p", 640, 360, 600, 64)
    ];

    public record QualityProfile(
        string Name,
        int Width,
        int Height,
        int VideoBitrateKbps,
        int AudioBitrateKbps);
}
