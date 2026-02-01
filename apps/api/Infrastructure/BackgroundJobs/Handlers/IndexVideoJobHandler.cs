using Microsoft.EntityFrameworkCore;
using T4L.VideoSearch.Api.Domain.Entities;
using T4L.VideoSearch.Api.Infrastructure.Adapters;
using T4L.VideoSearch.Api.Infrastructure.BackgroundJobs.Jobs;
using T4L.VideoSearch.Api.Infrastructure.Persistence;

namespace T4L.VideoSearch.Api.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Handles search indexing jobs - indexes video transcripts for search
/// In production, this integrates with Azure AI Search
/// </summary>
public class IndexVideoJobHandler : IJobHandler<IndexVideoJob>
{
    private readonly AppDbContext _dbContext;
    private readonly ISearchIndexClient _searchIndexClient;
    private readonly ILogger<IndexVideoJobHandler> _logger;

    public IndexVideoJobHandler(
        AppDbContext dbContext,
        ISearchIndexClient searchIndexClient,
        ILogger<IndexVideoJobHandler> logger)
    {
        _dbContext = dbContext;
        _searchIndexClient = searchIndexClient;
        _logger = logger;
    }

    public async Task HandleAsync(IndexVideoJob job, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Starting search indexing for VideoAsset {VideoAssetId}",
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
                .FirstOrDefaultAsync(v => v.Id == job.VideoAssetId, cancellationToken);

            if (video == null)
            {
                throw new Exception($"VideoAsset {job.VideoAssetId} not found");
            }

            // Delete any existing segments for this video (re-index scenario)
            await _searchIndexClient.DeleteVideoSegmentsAsync(video.Id, cancellationToken);

            // Create search documents from transcript segments
            var searchDocuments = video.TranscriptSegments.Select(segment => new SearchDocument(
                Id: segment.Id.ToString(),
                VideoId: video.Id,
                VideoTitle: video.Title,
                StartMs: segment.StartMs,
                EndMs: segment.EndMs,
                Text: segment.Text,
                Language: segment.DetectedLanguage ?? video.LanguageHint,
                // Published videos are visible to everyone; otherwise restrict to explicit allow-list or owner
                AllowedGroupIds: video.Status == VideoStatus.Published
                    ? Array.Empty<string>()
                    : video.AllowedGroupIds,
                AllowedUserOids: video.Status == VideoStatus.Published
                    ? Array.Empty<string>()
                    : video.AllowedUserOids.Length > 0
                        ? video.AllowedUserOids
                        : new[] { video.CreatedByOid }, // Owner always has access
                Vector: null // Would be populated by embedding service in production
            )).ToList();

            // Index all segments
            if (searchDocuments.Any())
            {
                await _searchIndexClient.IndexSegmentsAsync(searchDocuments, cancellationToken);
            }

            _logger.LogInformation(
                "Indexed video {VideoId}: Title='{Title}', Segments={SegmentCount}",
                video.Id,
                video.Title,
                searchDocuments.Count);

            // Mark job as completed
            processingJob.Status = JobStatus.Completed;
            processingJob.CompletedAt = DateTime.UtcNow;
            processingJob.UpdatedAt = DateTime.UtcNow;

            // Update video status to Published unless it was quarantined/rejected
            if (video.Status != VideoStatus.Quarantined && video.Status != VideoStatus.Rejected)
            {
                video.Status = VideoStatus.Published;
                video.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Search indexing completed for VideoAsset {VideoAssetId}, status changed to Published",
                job.VideoAssetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Search indexing failed for VideoAsset {VideoAssetId}",
                job.VideoAssetId);

            processingJob.Status = JobStatus.Failed;
            processingJob.LastError = ex.Message;
            processingJob.UpdatedAt = DateTime.UtcNow;

            // Update video status to Failed
            var video = await _dbContext.VideoAssets
                .FirstOrDefaultAsync(v => v.Id == job.VideoAssetId, cancellationToken);

            if (video != null)
            {
                video.Status = VideoStatus.Failed;
                video.UpdatedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
