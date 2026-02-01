using Microsoft.EntityFrameworkCore;
using T4L.VideoSearch.Api.Domain.Entities;
using T4L.VideoSearch.Api.Infrastructure.BackgroundJobs.Jobs;
using T4L.VideoSearch.Api.Infrastructure.Persistence;
using T4L.VideoSearch.Api.Infrastructure.Services;

namespace T4L.VideoSearch.Api.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Handles encoding a video to a specific quality variant
/// </summary>
public class EncodeVideoVariantJobHandler : IJobHandler<EncodeVideoVariantJob>
{
    private readonly AppDbContext _dbContext;
    private readonly IEncodingService _encodingService;
    private readonly IJobQueue _jobQueue;
    private readonly ILogger<EncodeVideoVariantJobHandler> _logger;

    public EncodeVideoVariantJobHandler(
        AppDbContext dbContext,
        IEncodingService encodingService,
        IJobQueue jobQueue,
        ILogger<EncodeVideoVariantJobHandler> logger)
    {
        _dbContext = dbContext;
        _encodingService = encodingService;
        _jobQueue = jobQueue;
        _logger = logger;
    }

    public async Task HandleAsync(EncodeVideoVariantJob job, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Encoding video {VideoAssetId} to {Quality} variant",
            job.VideoAssetId, job.Quality);

        var variant = await _dbContext.VideoVariants
            .FirstOrDefaultAsync(v => v.Id == job.VariantId, cancellationToken);

        if (variant == null)
        {
            _logger.LogWarning("VideoVariant {VariantId} not found", job.VariantId);
            return;
        }

        // Update status to Encoding with initial progress
        variant.Status = VariantStatus.Encoding;
        variant.Progress = 0;
        variant.ProgressMessage = $"Starting {job.Quality} encoding...";
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            // Update progress - downloading
            variant.Progress = 10;
            variant.ProgressMessage = "Downloading source video...";
            await _dbContext.SaveChangesAsync(cancellationToken);

            var profile = new QualityProfiles.QualityProfile(
                job.Quality,
                job.Width,
                job.Height,
                job.VideoBitrateKbps,
                job.AudioBitrateKbps);

            // Update progress - encoding started
            variant.Progress = 20;
            variant.ProgressMessage = $"Encoding to {job.Quality}...";
            await _dbContext.SaveChangesAsync(cancellationToken);

            var result = await _encodingService.EncodeToVariantAsync(
                job.BlobPath,
                job.VideoAssetId,
                profile,
                cancellationToken);

            if (result.Success)
            {
                variant.Status = VariantStatus.Completed;
                variant.Progress = 100;
                variant.ProgressMessage = "Encoding complete";
                variant.PlaylistPath = result.PlaylistPath;
                variant.SegmentsPath = result.SegmentsPath;
                variant.FileSizeBytes = result.FileSizeBytes;
                variant.CompletedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Successfully encoded video {VideoAssetId} to {Quality}",
                    job.VideoAssetId, job.Quality);
            }
            else
            {
                variant.Status = VariantStatus.Failed;
                variant.Progress = 0;
                variant.ProgressMessage = "Encoding failed";
                variant.ErrorMessage = result.Error;

                _logger.LogError(
                    "Failed to encode video {VideoAssetId} to {Quality}: {Error}",
                    job.VideoAssetId, job.Quality, result.Error);
            }
        }
        catch (Exception ex)
        {
            variant.Status = VariantStatus.Failed;
            variant.Progress = 0;
            variant.ProgressMessage = "Encoding failed with error";
            variant.ErrorMessage = ex.Message;

            _logger.LogError(ex,
                "Error encoding video {VideoAssetId} to {Quality}",
                job.VideoAssetId, job.Quality);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Check if all variants are complete, then generate master playlist
        await CheckAndGenerateMasterPlaylistAsync(job.VideoAssetId, cancellationToken);
    }

    private async Task CheckAndGenerateMasterPlaylistAsync(Guid videoAssetId, CancellationToken cancellationToken)
    {
        var variants = await _dbContext.VideoVariants
            .Where(v => v.VideoId == videoAssetId)
            .ToListAsync(cancellationToken);

        var allComplete = variants.All(v =>
            v.Status == VariantStatus.Completed || v.Status == VariantStatus.Failed);

        if (allComplete && variants.Any(v => v.Status == VariantStatus.Completed))
        {
            _logger.LogInformation(
                "All variants complete for video {VideoAssetId}, generating master playlist",
                videoAssetId);

            await _jobQueue.EnqueueAsync(new GenerateMasterPlaylistJob
            {
                VideoAssetId = videoAssetId
            }, cancellationToken);
        }
    }
}
