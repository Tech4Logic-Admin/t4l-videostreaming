using Microsoft.EntityFrameworkCore;
using T4L.VideoSearch.Api.Domain.Entities;
using T4L.VideoSearch.Api.Infrastructure.BackgroundJobs.Jobs;
using T4L.VideoSearch.Api.Infrastructure.Persistence;
using T4L.VideoSearch.Api.Infrastructure.Services;

namespace T4L.VideoSearch.Api.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Handles video transcription jobs
/// </summary>
public class TranscribeVideoJobHandler : IJobHandler<TranscribeVideoJob>
{
    private readonly AppDbContext _dbContext;
    private readonly ITranscriptionService _transcriptionService;
    private readonly IJobQueue _jobQueue;
    private readonly ILogger<TranscribeVideoJobHandler> _logger;
    private const int MaxRetries = 3;

    public TranscribeVideoJobHandler(
        AppDbContext dbContext,
        ITranscriptionService transcriptionService,
        IJobQueue jobQueue,
        ILogger<TranscribeVideoJobHandler> logger)
    {
        _dbContext = dbContext;
        _transcriptionService = transcriptionService;
        _jobQueue = jobQueue;
        _logger = logger;
    }

    public async Task HandleAsync(TranscribeVideoJob job, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting transcription for VideoAsset {VideoAssetId}",
            job.VideoAssetId);

        // Get the processing job record
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
        processingJob.Progress = 0;
        processingJob.ProgressMessage = "Starting transcription...";
        processingJob.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Update video status
        var video = await _dbContext.VideoAssets
            .FirstOrDefaultAsync(v => v.Id == job.VideoAssetId, cancellationToken);

        if (video != null)
        {
            video.Status = VideoStatus.Indexing;
            video.UpdatedAt = DateTime.UtcNow;
        }

        try
        {
            // Update progress: downloading
            processingJob.Progress = 10;
            processingJob.ProgressMessage = "Downloading video for transcription...";
            processingJob.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Perform transcription
            var result = await _transcriptionService.TranscribeAsync(
                job.BlobPath,
                job.LanguageHint,
                cancellationToken);

            // Update progress: processing result
            processingJob.Progress = 80;
            processingJob.ProgressMessage = "Processing transcript segments...";
            processingJob.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (!result.Success)
            {
                throw new Exception(result.Error ?? "Transcription failed");
            }

            // Save transcript segments
            var segments = result.Segments.Select(s => new TranscriptSegment
            {
                Id = Guid.NewGuid(),
                VideoId = job.VideoAssetId,
                StartMs = s.StartMs,
                EndMs = s.EndMs,
                Text = s.Text,
                DetectedLanguage = result.DetectedLanguage,
                Speaker = s.Speaker,
                Confidence = s.Confidence,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            _dbContext.TranscriptSegments.AddRange(segments);

            // Update video with duration if detected
            if (video != null && result.DurationMs.HasValue)
            {
                video.DurationMs = result.DurationMs;
            }

            // Mark job as completed
            processingJob.Status = JobStatus.Completed;
            processingJob.Progress = 100;
            processingJob.ProgressMessage = "Transcription complete";
            processingJob.CompletedAt = DateTime.UtcNow;
            processingJob.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Transcription completed for VideoAsset {VideoAssetId}: {SegmentCount} segments",
                job.VideoAssetId,
                segments.Count);

            // Check if prerequisites are done and trigger content moderation
            await TriggerContentModerationIfReady(job.VideoAssetId, cancellationToken);

            // Trigger AI highlights extraction
            await TriggerAIHighlightsExtraction(job.VideoAssetId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Transcription failed for VideoAsset {VideoAssetId}, attempt {Attempt}",
                job.VideoAssetId,
                processingJob.Attempts);

            processingJob.LastError = ex.Message;
            processingJob.UpdatedAt = DateTime.UtcNow;

            if (processingJob.Attempts >= MaxRetries)
            {
                processingJob.Status = JobStatus.Failed;

                if (video != null)
                {
                    video.Status = VideoStatus.Failed;
                    video.UpdatedAt = DateTime.UtcNow;
                }
            }
            else
            {
                processingJob.Status = JobStatus.Pending;
                // Re-enqueue for retry
                await _jobQueue.EnqueueAsync(job, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task TriggerContentModerationIfReady(Guid videoAssetId, CancellationToken cancellationToken)
    {
        // Check if transcription and thumbnail are done - then we can start moderation
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

    private async Task TriggerAIHighlightsExtraction(Guid videoAssetId, CancellationToken cancellationToken)
    {
        var aiHighlightsJob = await _dbContext.VideoProcessingJobs
            .FirstOrDefaultAsync(j =>
                j.VideoId == videoAssetId &&
                j.Stage == ProcessingStage.AIHighlights &&
                j.Status == JobStatus.Pending,
                cancellationToken);

        if (aiHighlightsJob != null)
        {
            _logger.LogInformation(
                "Triggering AI highlights extraction for VideoAsset {VideoAssetId}",
                videoAssetId);

            await _jobQueue.EnqueueAsync(new ExtractHighlightsJob
            {
                JobId = Guid.NewGuid(),
                VideoAssetId = videoAssetId,
                ProcessingJobId = aiHighlightsJob.Id
            }, cancellationToken);
        }
    }
}
