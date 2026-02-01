using T4L.VideoSearch.Api.Domain.Entities;

namespace T4L.VideoSearch.Api.Infrastructure.BackgroundJobs.Jobs;

/// <summary>
/// Job to process a newly uploaded video through the pipeline
/// </summary>
public class ProcessVideoJob : IJob
{
    public Guid JobId { get; init; } = Guid.NewGuid();
    public Guid VideoAssetId { get; init; }
    public string BlobPath { get; init; } = string.Empty;
    public string? LanguageHint { get; init; }
}

/// <summary>
/// Job to transcribe a video
/// </summary>
public class TranscribeVideoJob : IJob
{
    public Guid JobId { get; init; } = Guid.NewGuid();
    public Guid VideoAssetId { get; init; }
    public Guid ProcessingJobId { get; init; }
    public string BlobPath { get; init; } = string.Empty;
    public string? LanguageHint { get; init; }
}

/// <summary>
/// Job to generate thumbnail for a video
/// </summary>
public class GenerateThumbnailJob : IJob
{
    public Guid JobId { get; init; } = Guid.NewGuid();
    public Guid VideoAssetId { get; init; }
    public Guid ProcessingJobId { get; init; }
    public string BlobPath { get; init; } = string.Empty;
}

/// <summary>
/// Job to index video in search
/// </summary>
public class IndexVideoJob : IJob
{
    public Guid JobId { get; init; } = Guid.NewGuid();
    public Guid VideoAssetId { get; init; }
    public Guid ProcessingJobId { get; init; }
}

/// <summary>
/// Job to perform content moderation on video metadata and transcript
/// </summary>
public class ContentModerationJob : IJob
{
    public Guid JobId { get; init; } = Guid.NewGuid();
    public Guid VideoAssetId { get; init; }
    public Guid ProcessingJobId { get; init; }
}

/// <summary>
/// Job to encode video to a specific quality variant for ABR streaming
/// </summary>
public class EncodeVideoVariantJob : IJob
{
    public Guid JobId { get; init; } = Guid.NewGuid();
    public Guid VideoAssetId { get; init; }
    public Guid VariantId { get; init; }
    public string BlobPath { get; init; } = string.Empty;
    public string Quality { get; init; } = string.Empty;
    public int Width { get; init; }
    public int Height { get; init; }
    public int VideoBitrateKbps { get; init; }
    public int AudioBitrateKbps { get; init; }
}

/// <summary>
/// Job to generate master HLS playlist after all variants are encoded
/// </summary>
public class GenerateMasterPlaylistJob : IJob
{
    public Guid JobId { get; init; } = Guid.NewGuid();
    public Guid VideoAssetId { get; init; }
}

/// <summary>
/// Job to extract AI-powered highlights and summary from video transcript
/// </summary>
public class ExtractHighlightsJob : IJob
{
    public Guid JobId { get; init; } = Guid.NewGuid();
    public Guid VideoAssetId { get; init; }
    public Guid ProcessingJobId { get; init; }
}
