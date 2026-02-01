using T4L.VideoSearch.Api.Domain.Entities;

namespace T4L.VideoSearch.Api.Infrastructure.Services;

/// <summary>
/// Service for transcribing video/audio content
/// </summary>
public interface ITranscriptionService
{
    /// <summary>
    /// Transcribe a video from blob storage
    /// </summary>
    /// <param name="blobPath">Path to the video in blob storage</param>
    /// <param name="languageHint">Optional language hint (ISO code)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Transcript result with segments</returns>
    Task<TranscriptResult> TranscribeAsync(
        string blobPath,
        string? languageHint = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a transcription operation
/// </summary>
public class TranscriptResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? DetectedLanguage { get; init; }
    public long? DurationMs { get; init; }
    public List<TranscriptSegmentData> Segments { get; init; } = [];
}

/// <summary>
/// Data for a single transcript segment
/// </summary>
public record TranscriptSegmentData
{
    public long StartMs { get; init; }
    public long EndMs { get; init; }
    public string Text { get; init; } = string.Empty;
    public string? Speaker { get; init; }
    public float? Confidence { get; init; }
}
