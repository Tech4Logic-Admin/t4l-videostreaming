using T4L.VideoSearch.Api.Domain.Entities;

namespace T4L.VideoSearch.Api.Infrastructure.Services;

/// <summary>
/// Service for transcoding videos to multiple quality variants
/// </summary>
public interface IEncodingService
{
    /// <summary>
    /// Encode a video to the specified quality profile
    /// </summary>
    /// <param name="sourceBlobPath">Path to the source video in blob storage</param>
    /// <param name="videoId">Video asset ID</param>
    /// <param name="profile">Quality profile to encode to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Encoding result with paths to HLS segments</returns>
    Task<EncodingResult> EncodeToVariantAsync(
        string sourceBlobPath,
        Guid videoId,
        QualityProfiles.QualityProfile profile,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate the master HLS playlist that references all variants
    /// </summary>
    /// <param name="videoId">Video asset ID</param>
    /// <param name="variants">List of completed variants</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Path to the master playlist</returns>
    Task<string> GenerateMasterPlaylistAsync(
        Guid videoId,
        IEnumerable<VideoVariant> variants,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get video metadata (duration, resolution, etc.)
    /// </summary>
    Task<VideoMetadata> GetVideoMetadataAsync(
        string blobPath,
        CancellationToken cancellationToken = default);
}

public record EncodingResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string PlaylistPath { get; init; } = string.Empty;
    public string SegmentsPath { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public int SegmentCount { get; init; }
}

public record VideoMetadata
{
    public int Width { get; init; }
    public int Height { get; init; }
    public long DurationMs { get; init; }
    public string Codec { get; init; } = string.Empty;
    public int BitrateKbps { get; init; }
    public double FrameRate { get; init; }
}
