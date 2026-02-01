using Microsoft.EntityFrameworkCore;
using T4L.VideoSearch.Api.Domain.Entities;
using T4L.VideoSearch.Api.Infrastructure.BackgroundJobs.Jobs;
using T4L.VideoSearch.Api.Infrastructure.Persistence;

namespace T4L.VideoSearch.Api.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Handles the initial video processing job - creates processing jobs for each stage
/// </summary>
public class ProcessVideoJobHandler : IJobHandler<ProcessVideoJob>
{
    private readonly AppDbContext _dbContext;
    private readonly IJobQueue _jobQueue;
    private readonly ILogger<ProcessVideoJobHandler> _logger;

    public ProcessVideoJobHandler(
        AppDbContext dbContext,
        IJobQueue jobQueue,
        ILogger<ProcessVideoJobHandler> logger)
    {
        _dbContext = dbContext;
        _jobQueue = jobQueue;
        _logger = logger;
    }

    public async Task HandleAsync(ProcessVideoJob job, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting video processing pipeline for VideoAsset {VideoAssetId}",
            job.VideoAssetId);

        // Get the video asset
        var video = await _dbContext.VideoAssets
            .FirstOrDefaultAsync(v => v.Id == job.VideoAssetId, cancellationToken);

        if (video == null)
        {
            _logger.LogWarning("VideoAsset {VideoAssetId} not found", job.VideoAssetId);
            return;
        }

        // Update status to Queued
        video.Status = VideoStatus.Queued;
        video.UpdatedAt = DateTime.UtcNow;

        // Create processing jobs for each stage
        var processingJobs = new List<VideoProcessingJob>
        {
            // Malware scan (skipped in dev mode - would integrate with Defender API)
            new()
            {
                Id = Guid.NewGuid(),
                VideoId = job.VideoAssetId,
                Stage = ProcessingStage.MalwareScan,
                Status = JobStatus.Skipped, // Skip in dev mode
                CompletedAt = DateTime.UtcNow
            },
            // Content moderation - analyzes title, description, and transcript
            new()
            {
                Id = Guid.NewGuid(),
                VideoId = job.VideoAssetId,
                Stage = ProcessingStage.ContentModeration,
                Status = JobStatus.Pending
            },
            // Transcription job
            new()
            {
                Id = Guid.NewGuid(),
                VideoId = job.VideoAssetId,
                Stage = ProcessingStage.Transcription,
                Status = JobStatus.Pending
            },
            // Thumbnail generation job
            new()
            {
                Id = Guid.NewGuid(),
                VideoId = job.VideoAssetId,
                Stage = ProcessingStage.ThumbnailGeneration,
                Status = JobStatus.Pending
            },
            // Search indexing job (will run after transcription)
            new()
            {
                Id = Guid.NewGuid(),
                VideoId = job.VideoAssetId,
                Stage = ProcessingStage.SearchIndexing,
                Status = JobStatus.Pending
            },
            // AI Highlights extraction (runs after transcription)
            new()
            {
                Id = Guid.NewGuid(),
                VideoId = job.VideoAssetId,
                Stage = ProcessingStage.AIHighlights,
                Status = JobStatus.Pending
            },
            // Encoding job for ABR streaming
            new()
            {
                Id = Guid.NewGuid(),
                VideoId = job.VideoAssetId,
                Stage = ProcessingStage.Encoding,
                Status = JobStatus.Pending
            }
        };

        _dbContext.VideoProcessingJobs.AddRange(processingJobs);

        // Create video variants for each quality profile
        var variants = QualityProfiles.All.Select(profile => new VideoVariant
        {
            Id = Guid.NewGuid(),
            VideoId = job.VideoAssetId,
            Quality = profile.Name,
            Width = profile.Width,
            Height = profile.Height,
            VideoBitrateKbps = profile.VideoBitrateKbps,
            AudioBitrateKbps = profile.AudioBitrateKbps,
            Status = VariantStatus.Pending
        }).ToList();

        _dbContext.VideoVariants.AddRange(variants);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created {Count} processing jobs and {VariantCount} variants for VideoAsset {VideoAssetId}",
            processingJobs.Count,
            variants.Count,
            job.VideoAssetId);

        // Enqueue transcription job
        var transcriptionJob = processingJobs.First(j => j.Stage == ProcessingStage.Transcription);
        await _jobQueue.EnqueueAsync(new TranscribeVideoJob
        {
            VideoAssetId = job.VideoAssetId,
            ProcessingJobId = transcriptionJob.Id,
            BlobPath = job.BlobPath,
            LanguageHint = job.LanguageHint
        }, cancellationToken);

        // Enqueue thumbnail job
        var thumbnailJob = processingJobs.First(j => j.Stage == ProcessingStage.ThumbnailGeneration);
        await _jobQueue.EnqueueAsync(new GenerateThumbnailJob
        {
            VideoAssetId = job.VideoAssetId,
            ProcessingJobId = thumbnailJob.Id,
            BlobPath = job.BlobPath
        }, cancellationToken);

        // Enqueue encoding jobs for each quality variant
        var encodingProcessingJob = processingJobs.First(j => j.Stage == ProcessingStage.Encoding);
        encodingProcessingJob.Status = JobStatus.InProgress;
        encodingProcessingJob.StartedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var variant in variants)
        {
            await _jobQueue.EnqueueAsync(new EncodeVideoVariantJob
            {
                VideoAssetId = job.VideoAssetId,
                VariantId = variant.Id,
                BlobPath = job.BlobPath,
                Quality = variant.Quality,
                Width = variant.Width,
                Height = variant.Height,
                VideoBitrateKbps = variant.VideoBitrateKbps,
                AudioBitrateKbps = variant.AudioBitrateKbps
            }, cancellationToken);
        }
    }
}
