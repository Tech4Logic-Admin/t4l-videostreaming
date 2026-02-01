using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using T4L.VideoSearch.Api.Domain.Entities;
using T4L.VideoSearch.Api.Infrastructure.BackgroundJobs.Jobs;
using T4L.VideoSearch.Api.Infrastructure.Persistence;

namespace T4L.VideoSearch.Api.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Handles thumbnail generation jobs (mock implementation for dev)
/// </summary>
public class GenerateThumbnailJobHandler : IJobHandler<GenerateThumbnailJob>
{
    private readonly AppDbContext _dbContext;
    private readonly IJobQueue _jobQueue;
    private readonly ILogger<GenerateThumbnailJobHandler> _logger;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly Random _random = new();

    public GenerateThumbnailJobHandler(
        AppDbContext dbContext,
        IJobQueue jobQueue,
        BlobServiceClient blobServiceClient,
        ILogger<GenerateThumbnailJobHandler> logger)
    {
        _dbContext = dbContext;
        _jobQueue = jobQueue;
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task HandleAsync(GenerateThumbnailJob job, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting thumbnail generation for VideoAsset {VideoAssetId}",
            job.VideoAssetId);

        var processingJob = await _dbContext.VideoProcessingJobs
            .FirstOrDefaultAsync(j => j.Id == job.ProcessingJobId, cancellationToken);

        if (processingJob == null)
        {
            _logger.LogWarning("ProcessingJob {ProcessingJobId} not found", job.ProcessingJobId);
            return;
        }

        // Update job status
        processingJob.Status = JobStatus.InProgress;
        processingJob.StartedAt = DateTime.UtcNow;
        processingJob.Attempts++;
        processingJob.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            // Simulate thumbnail generation (would use FFmpeg in production)
            await Task.Delay(_random.Next(500, 2000), cancellationToken);

            // Generate mock thumbnail image and upload to blob storage
            var thumbnailPath = $"thumbnails/{job.VideoAssetId}.png";
            await UploadPlaceholderThumbnailAsync(thumbnailPath, cancellationToken);

            // Update video asset with thumbnail path
            var video = await _dbContext.VideoAssets
                .FirstOrDefaultAsync(v => v.Id == job.VideoAssetId, cancellationToken);

            if (video != null)
            {
                video.ThumbnailPath = thumbnailPath;
                video.UpdatedAt = DateTime.UtcNow;
            }

            // Mark job as completed
            processingJob.Status = JobStatus.Completed;
            processingJob.CompletedAt = DateTime.UtcNow;
            processingJob.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Thumbnail generation completed for VideoAsset {VideoAssetId}: {ThumbnailPath}",
                job.VideoAssetId,
                thumbnailPath);

            // Check if all prerequisite jobs are done and trigger content moderation
            await TriggerContentModerationIfReady(job.VideoAssetId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Thumbnail generation failed for VideoAsset {VideoAssetId}",
                job.VideoAssetId);

            processingJob.Status = JobStatus.Failed;
            processingJob.LastError = ex.Message;
            processingJob.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task UploadPlaceholderThumbnailAsync(string blobPath, CancellationToken cancellationToken)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient("thumbnails");
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var blobClient = containerClient.GetBlobClient(blobPath);

        // Tiny 1x1 gray PNG placeholder (valid PNG bytes)
        var pngBytes = PlaceholderPng1x1Gray;
        await using var ms = new MemoryStream(pngBytes);
        await blobClient.UploadAsync(ms, overwrite: true, cancellationToken: cancellationToken);
    }

    // Minimal valid 1x1 PNG (gray pixel)
    private static readonly byte[] PlaceholderPng1x1Gray = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMBAWgm4NwAAAAASUVORK5CYII=");

    private async Task TriggerContentModerationIfReady(Guid videoAssetId, CancellationToken cancellationToken)
    {
        var jobs = await _dbContext.VideoProcessingJobs
            .Where(j => j.VideoId == videoAssetId)
            .ToListAsync(cancellationToken);

        var transcriptionDone = jobs.Any(j =>
            j.Stage == ProcessingStage.Transcription &&
            (j.Status == JobStatus.Completed || j.Status == JobStatus.Skipped));

        var thumbnailDone = jobs.Any(j =>
            j.Stage == ProcessingStage.ThumbnailGeneration &&
            (j.Status == JobStatus.Completed || j.Status == JobStatus.Skipped));

        var moderationJob = jobs.FirstOrDefault(j =>
            j.Stage == ProcessingStage.ContentModeration &&
            j.Status == JobStatus.Pending);

        if (transcriptionDone && thumbnailDone && moderationJob != null)
        {
            _logger.LogInformation(
                "Prerequisites met, triggering content moderation for VideoAsset {VideoAssetId}",
                videoAssetId);

            await _jobQueue.EnqueueAsync(new ContentModerationJob
            {
                JobId = Guid.NewGuid(),
                VideoAssetId = videoAssetId,
                ProcessingJobId = moderationJob.Id
            }, cancellationToken);
        }
    }
}
