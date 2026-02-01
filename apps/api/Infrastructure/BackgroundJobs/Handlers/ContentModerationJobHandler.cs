using Microsoft.EntityFrameworkCore;
using T4L.VideoSearch.Api.Domain.Entities;
using T4L.VideoSearch.Api.Infrastructure.Adapters;
using T4L.VideoSearch.Api.Infrastructure.BackgroundJobs;
using T4L.VideoSearch.Api.Infrastructure.BackgroundJobs.Jobs;
using T4L.VideoSearch.Api.Infrastructure.Persistence;

namespace T4L.VideoSearch.Api.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Handles content moderation for videos - analyzes title, description, and transcript
/// </summary>
public class ContentModerationJobHandler : IJobHandler<ContentModerationJob>
{
    private readonly AppDbContext _dbContext;
    private readonly IContentSafetyClient _contentSafetyClient;
    private readonly IJobQueue _jobQueue;
    private readonly ILogger<ContentModerationJobHandler> _logger;

    // Threshold for auto-quarantine
    private const float HighSeverityThreshold = 0.7f;

    public ContentModerationJobHandler(
        AppDbContext dbContext,
        IContentSafetyClient contentSafetyClient,
        IJobQueue jobQueue,
        ILogger<ContentModerationJobHandler> logger)
    {
        _dbContext = dbContext;
        _contentSafetyClient = contentSafetyClient;
        _jobQueue = jobQueue;
        _logger = logger;
    }

    public async Task HandleAsync(ContentModerationJob job, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting content moderation for VideoAsset {VideoAssetId}",
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
            // Get video with transcripts
            var video = await _dbContext.VideoAssets
                .Include(v => v.TranscriptSegments)
                .Include(v => v.ModerationResult)
                .FirstOrDefaultAsync(v => v.Id == job.VideoAssetId, cancellationToken);

            if (video == null)
            {
                throw new Exception($"VideoAsset {job.VideoAssetId} not found");
            }

            // Update video status to Moderating
            video.Status = VideoStatus.Moderating;
            video.UpdatedAt = DateTime.UtcNow;

            // Create or update moderation result
            var moderationResult = video.ModerationResult ?? new ModerationResult
            {
                Id = Guid.NewGuid(),
                VideoId = video.Id,
                CreatedAt = DateTime.UtcNow
            };

            if (video.ModerationResult == null)
            {
                _dbContext.ModerationResults.Add(moderationResult);
            }

            var allReasons = new List<string>();
            var highestSeverity = ModerationSeverity.Low;

            // Analyze title
            var titleResult = await AnalyzeTextAsync(video.Title, "Title", cancellationToken);
            if (!titleResult.IsSafe)
            {
                allReasons.AddRange(titleResult.Reasons);
                highestSeverity = GetHigherSeverity(highestSeverity, titleResult.Severity);
            }

            // Analyze description if present
            if (!string.IsNullOrWhiteSpace(video.Description))
            {
                var descResult = await AnalyzeTextAsync(video.Description, "Description", cancellationToken);
                if (!descResult.IsSafe)
                {
                    allReasons.AddRange(descResult.Reasons);
                    highestSeverity = GetHigherSeverity(highestSeverity, descResult.Severity);
                }
            }

            // Analyze transcript segments (sample if too many)
            var segments = video.TranscriptSegments.ToList();
            var segmentsToAnalyze = segments.Count > 20
                ? SampleSegments(segments, 20)
                : segments;

            foreach (var segment in segmentsToAnalyze)
            {
                var segmentResult = await AnalyzeTextAsync(segment.Text, $"Transcript@{segment.StartMs}ms", cancellationToken);
                if (!segmentResult.IsSafe)
                {
                    allReasons.AddRange(segmentResult.Reasons);
                    highestSeverity = GetHigherSeverity(highestSeverity, segmentResult.Severity);
                }
            }

            // Update moderation result
            moderationResult.ContentSafetyStatus = allReasons.Any()
                ? ContentSafetyStatus.Flagged
                : ContentSafetyStatus.Safe;
            moderationResult.Reasons = allReasons.Distinct().ToArray();
            moderationResult.HighestSeverity = allReasons.Any() ? highestSeverity : null;

            // Determine video status based on moderation results
            if (allReasons.Any())
            {
                // Flagged content - needs manual review
                video.Status = VideoStatus.Quarantined;
                processingJob.Status = JobStatus.Completed;

                await MarkSearchJobSkippedAsync(video.Id, cancellationToken);

                _logger.LogWarning(
                    "Video {VideoId} flagged for moderation review: {Reasons}",
                    video.Id,
                    string.Join(", ", allReasons.Take(5)));
            }
            else
            {
                // Safe content - continue processing
                video.Status = VideoStatus.Indexing;
                processingJob.Status = JobStatus.Completed;

                await EnqueueSearchIndexingAsync(video.Id, cancellationToken);

                _logger.LogInformation(
                    "Video {VideoId} passed content moderation",
                    video.Id);
            }

            processingJob.CompletedAt = DateTime.UtcNow;
            processingJob.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Content moderation completed for VideoAsset {VideoAssetId}, Status: {Status}",
                job.VideoAssetId, video.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Content moderation failed for VideoAsset {VideoAssetId}",
                job.VideoAssetId);

            processingJob.Status = JobStatus.Failed;
            processingJob.LastError = ex.Message;
            processingJob.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<(bool IsSafe, List<string> Reasons, ModerationSeverity Severity)> AnalyzeTextAsync(
        string text,
        string context,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _contentSafetyClient.AnalyzeTextAsync(text, cancellationToken);

            if (!result.IsSafe)
            {
                var reasons = result.Categories
                    .Where(c => c.Severity != ContentSafetySeverity.None)
                    .Select(c => $"{context}: {c.Category} ({c.Severity})")
                    .ToList();

                var severity = MapSeverity(result.OverallSeverity);
                return (false, reasons, severity);
            }

            return (true, new List<string>(), ModerationSeverity.Low);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to analyze {Context}", context);
            return (true, new List<string>(), ModerationSeverity.Low); // Fail open in case of errors
        }
    }

    private static ModerationSeverity MapSeverity(ContentSafetySeverity severity)
    {
        return severity switch
        {
            ContentSafetySeverity.High => ModerationSeverity.High,
            ContentSafetySeverity.Medium => ModerationSeverity.Medium,
            _ => ModerationSeverity.Low
        };
    }

    private static ModerationSeverity GetHigherSeverity(ModerationSeverity a, ModerationSeverity b)
    {
        return (ModerationSeverity)Math.Max((int)a, (int)b);
    }

    private static List<TranscriptSegment> SampleSegments(List<TranscriptSegment> segments, int count)
    {
        // Evenly sample segments across the video
        var step = segments.Count / count;
        return Enumerable.Range(0, count)
            .Select(i => segments[Math.Min(i * step, segments.Count - 1)])
            .ToList();
    }

    private async Task EnqueueSearchIndexingAsync(Guid videoAssetId, CancellationToken cancellationToken)
    {
        var searchJob = await _dbContext.VideoProcessingJobs
            .FirstOrDefaultAsync(j =>
                j.VideoId == videoAssetId &&
                j.Stage == ProcessingStage.SearchIndexing,
                cancellationToken);

        if (searchJob == null)
        {
            searchJob = new VideoProcessingJob
            {
                Id = Guid.NewGuid(),
                VideoId = videoAssetId,
                Stage = ProcessingStage.SearchIndexing,
                Status = JobStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.VideoProcessingJobs.Add(searchJob);
        }
        else if (searchJob.Status != JobStatus.Completed)
        {
            searchJob.Status = JobStatus.Pending;
            searchJob.LastError = null;
            searchJob.Attempts = 0;
            searchJob.StartedAt = null;
            searchJob.CompletedAt = null;
            searchJob.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            return; // already indexed
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _jobQueue.EnqueueAsync(new IndexVideoJob
        {
            VideoAssetId = videoAssetId,
            ProcessingJobId = searchJob.Id
        }, cancellationToken);
    }

    private async Task MarkSearchJobSkippedAsync(Guid videoAssetId, CancellationToken cancellationToken)
    {
        var searchJob = await _dbContext.VideoProcessingJobs
            .FirstOrDefaultAsync(j =>
                j.VideoId == videoAssetId &&
                j.Stage == ProcessingStage.SearchIndexing,
                cancellationToken);

        if (searchJob != null && searchJob.Status != JobStatus.Completed)
        {
            searchJob.Status = JobStatus.Skipped;
            searchJob.LastError = "Flagged by content moderation";
            searchJob.CompletedAt = DateTime.UtcNow;
            searchJob.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
