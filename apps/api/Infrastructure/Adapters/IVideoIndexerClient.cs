namespace T4L.VideoSearch.Api.Infrastructure.Adapters;

/// <summary>
/// Abstraction for video indexing/transcription service
/// </summary>
public interface IVideoIndexerClient
{
    /// <summary>
    /// Submit a video for indexing and transcription
    /// </summary>
    Task<string> SubmitVideoAsync(VideoIndexRequest request, CancellationToken ct = default);

    /// <summary>
    /// Get the status of a video indexing job
    /// </summary>
    Task<VideoIndexStatus> GetStatusAsync(string jobId, CancellationToken ct = default);

    /// <summary>
    /// Get the transcription results
    /// </summary>
    Task<VideoIndexResult?> GetResultsAsync(string jobId, CancellationToken ct = default);
}

public record VideoIndexRequest(
    Guid VideoId,
    string VideoUrl,
    string? LanguageHint,
    string? CallbackUrl
);

public record VideoIndexStatus(
    string JobId,
    IndexingState State,
    int ProgressPercent,
    string? ErrorMessage
);

public enum IndexingState
{
    Queued,
    Processing,
    Completed,
    Failed
}

public record VideoIndexResult(
    string JobId,
    long DurationMs,
    string DetectedLanguage,
    IReadOnlyList<TranscriptItem> Transcript,
    IReadOnlyList<string> Keywords
);

public record TranscriptItem(
    long StartMs,
    long EndMs,
    string Text,
    string? Speaker,
    float Confidence
);
