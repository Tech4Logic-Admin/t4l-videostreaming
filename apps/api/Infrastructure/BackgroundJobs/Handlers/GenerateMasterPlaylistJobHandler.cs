using Microsoft.EntityFrameworkCore;
using T4L.VideoSearch.Api.Domain.Entities;
using T4L.VideoSearch.Api.Infrastructure.BackgroundJobs.Jobs;
using T4L.VideoSearch.Api.Infrastructure.Persistence;
using T4L.VideoSearch.Api.Infrastructure.Services;

namespace T4L.VideoSearch.Api.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Handles generating the master HLS playlist after all variants are encoded
/// </summary>
public class GenerateMasterPlaylistJobHandler : IJobHandler<GenerateMasterPlaylistJob>
{
    private readonly AppDbContext _dbContext;
    private readonly IEncodingService _encodingService;
    private readonly ILogger<GenerateMasterPlaylistJobHandler> _logger;

    public GenerateMasterPlaylistJobHandler(
        AppDbContext dbContext,
        IEncodingService encodingService,
        ILogger<GenerateMasterPlaylistJobHandler> logger)
    {
        _dbContext = dbContext;
        _encodingService = encodingService;
        _logger = logger;
    }

    public async Task HandleAsync(GenerateMasterPlaylistJob job, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Generating master playlist for video {VideoAssetId}",
            job.VideoAssetId);

        var video = await _dbContext.VideoAssets
            .Include(v => v.Variants)
            .FirstOrDefaultAsync(v => v.Id == job.VideoAssetId, cancellationToken);

        if (video == null)
        {
            _logger.LogWarning("VideoAsset {VideoAssetId} not found", job.VideoAssetId);
            return;
        }

        var completedVariants = video.Variants
            .Where(v => v.Status == VariantStatus.Completed)
            .ToList();

        if (!completedVariants.Any())
        {
            _logger.LogWarning(
                "No completed variants for video {VideoAssetId}",
                job.VideoAssetId);
            return;
        }

        try
        {
            var masterPlaylistPath = await _encodingService.GenerateMasterPlaylistAsync(
                job.VideoAssetId,
                completedVariants,
                cancellationToken);

            video.MasterPlaylistPath = masterPlaylistPath;
            video.UpdatedAt = DateTime.UtcNow;
            if (video.Status is VideoStatus.Indexing or VideoStatus.Queued)
            {
                video.Status = VideoStatus.Published;
            }

            // Update encoding processing job to completed
            var encodingJob = await _dbContext.VideoProcessingJobs
                .FirstOrDefaultAsync(j =>
                    j.VideoId == job.VideoAssetId &&
                    j.Stage == ProcessingStage.Encoding,
                    cancellationToken);

            if (encodingJob != null)
            {
                encodingJob.Status = JobStatus.Completed;
                encodingJob.CompletedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Master playlist generated for video {VideoAssetId}: {Path}",
                job.VideoAssetId, masterPlaylistPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to generate master playlist for video {VideoAssetId}",
                job.VideoAssetId);

            var encodingJob = await _dbContext.VideoProcessingJobs
                .FirstOrDefaultAsync(j =>
                    j.VideoId == job.VideoAssetId &&
                    j.Stage == ProcessingStage.Encoding,
                    cancellationToken);

            if (encodingJob != null)
            {
                encodingJob.Status = JobStatus.Failed;
                encodingJob.LastError = ex.Message;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
