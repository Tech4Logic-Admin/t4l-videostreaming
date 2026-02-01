using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using T4L.VideoSearch.Api.Auth;
using T4L.VideoSearch.Api.Domain.Entities;
using T4L.VideoSearch.Api.Infrastructure.Adapters;
using T4L.VideoSearch.Api.Infrastructure.BackgroundJobs;
using T4L.VideoSearch.Api.Infrastructure.BackgroundJobs.Jobs;
using T4L.VideoSearch.Api.Infrastructure.Persistence;
using T4L.VideoSearch.Api.Infrastructure.Services;

namespace T4L.VideoSearch.Api.Controllers;

/// <summary>
/// Video management endpoints with RBAC
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VideosController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<VideosController> _logger;
    private readonly IJobQueue _jobQueue;
    private readonly IBlobStore _blobStore;
    private readonly ITranslationService _translationService;

    public VideosController(
        AppDbContext dbContext,
        ICurrentUser currentUser,
        ILogger<VideosController> logger,
        IJobQueue jobQueue,
        IBlobStore blobStore,
        ITranslationService translationService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _logger = logger;
        _jobQueue = jobQueue;
        _blobStore = blobStore;
        _translationService = translationService;
    }

    /// <summary>
    /// Get all videos (filtered by user's access level)
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.CanViewVideos)]
    [ProducesResponseType(typeof(VideoListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VideoListResponse>> GetVideos(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.VideoAssets.AsQueryable();

        // Security trimming: non-admins and non-reviewers can only see published videos or their own
        if (!_currentUser.IsInAnyRole(Roles.Admin, Roles.Reviewer))
        {
            query = query.Where(v =>
                v.Status == VideoStatus.Published ||
                v.CreatedByOid == _currentUser.Id);
        }

        // Filter by status if specified
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<VideoStatus>(status, true, out var statusEnum))
        {
            query = query.Where(v => v.Status == statusEnum);
        }

        // Search by title
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(v => v.Title.ToLower().Contains(search.ToLower()));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var videoEntities = await query
            .OrderByDescending(v => v.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var videos = videoEntities.Select(v => new VideoSummaryDto
        {
            Id = v.Id,
            Title = v.Title,
            Description = v.Description,
            ThumbnailUrl = !string.IsNullOrEmpty(v.ThumbnailPath) ? $"{baseUrl}/api/stream/{v.Id}/thumbnail" : null,
            DurationMs = v.DurationMs,
            Status = v.Status.ToString(),
            CreatedAt = v.CreatedAt,
            CreatedByOid = v.CreatedByOid,
            LanguageHint = v.LanguageHint
        }).ToList();

        _logger.LogInformation("User {UserId} retrieved {Count} videos (page {Page})",
            _currentUser.Id, videos.Count, page);

        return Ok(new VideoListResponse
        {
            Videos = videos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    /// <summary>
    /// Get a specific video by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.CanViewVideos)]
    [ProducesResponseType(typeof(VideoDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<VideoDetailDto>> GetVideo(Guid id, CancellationToken cancellationToken)
    {
        var video = await _dbContext.VideoAssets
            .Include(v => v.TranscriptSegments.OrderBy(t => t.StartMs))
            .Include(v => v.Highlights.OrderByDescending(h => h.Importance ?? 0))
            .Include(v => v.Summary)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        if (video == null)
        {
            return NotFound();
        }

        // Security check: can user view this video?
        if (!CanUserViewVideo(video))
        {
            _logger.LogWarning("User {UserId} attempted to access video {VideoId} without permission",
                _currentUser.Id, id);
            return Forbid();
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var dto = new VideoDetailDto
        {
            Id = video.Id,
            Title = video.Title,
            Description = video.Description,
            BlobPath = video.BlobPath,
            ThumbnailUrl = !string.IsNullOrEmpty(video.ThumbnailPath) ? $"{baseUrl}/api/stream/{video.Id}/thumbnail" : null,
            DurationMs = video.DurationMs,
            Status = video.Status.ToString(),
            CreatedAt = video.CreatedAt,
            CreatedByOid = video.CreatedByOid,
            LanguageHint = video.LanguageHint,
            Tags = video.Tags,
            TranscriptSegments = video.TranscriptSegments.Select(t => new TranscriptSegmentDto
            {
                Id = t.Id,
                StartMs = t.StartMs,
                EndMs = t.EndMs,
                Text = t.Text,
                DetectedLanguage = t.DetectedLanguage,
                Speaker = t.Speaker,
                Confidence = t.Confidence
            }).ToList(),
            Highlights = video.Highlights.Select(h => new VideoHighlightDto
            {
                Id = h.Id,
                Text = h.Text,
                Category = h.Category,
                Importance = h.Importance,
                TimestampMs = h.TimestampMs
            }).ToList(),
            SummaryInfo = video.Summary != null ? new VideoSummaryInfoDto
            {
                Summary = video.Summary.Summary,
                TlDr = video.Summary.TlDr,
                Keywords = video.Summary.Keywords,
                Topics = video.Summary.Topics,
                Sentiment = video.Summary.Sentiment
            } : null
        };

        _logger.LogInformation("User {UserId} retrieved video {VideoId}", _currentUser.Id, id);

        return Ok(dto);
    }

    /// <summary>
    /// Get videos pending review (Reviewer/Admin only)
    /// </summary>
    [HttpGet("pending-review")]
    [Authorize(Policy = Policies.CanReview)]
    [ProducesResponseType(typeof(VideoListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VideoListResponse>> GetPendingReview(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // Moderating and Quarantined videos need review
        var query = _dbContext.VideoAssets
            .Where(v => v.Status == VideoStatus.Moderating || v.Status == VideoStatus.Quarantined);

        var totalCount = await query.CountAsync(cancellationToken);
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var videoEntities = await query
            .OrderBy(v => v.CreatedAt) // Oldest first for review queue
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var videos = videoEntities.Select(v => new VideoSummaryDto
        {
            Id = v.Id,
            Title = v.Title,
            Description = v.Description,
            ThumbnailUrl = !string.IsNullOrEmpty(v.ThumbnailPath) ? $"{baseUrl}/api/stream/{v.Id}/thumbnail" : null,
            DurationMs = v.DurationMs,
            Status = v.Status.ToString(),
            CreatedAt = v.CreatedAt,
            CreatedByOid = v.CreatedByOid,
            LanguageHint = v.LanguageHint
        }).ToList();

        _logger.LogInformation("User {UserId} (Reviewer) retrieved {Count} pending videos",
            _currentUser.Id, videos.Count);

        return Ok(new VideoListResponse
        {
            Videos = videos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    /// <summary>
    /// Get current user's own videos
    /// </summary>
    [HttpGet("my")]
    [Authorize]
    [ProducesResponseType(typeof(VideoListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VideoListResponse>> GetMyVideos(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.VideoAssets
            .Where(v => v.CreatedByOid == _currentUser.Id);

        // Filter by status if specified
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<VideoStatus>(status, true, out var statusEnum))
        {
            query = query.Where(v => v.Status == statusEnum);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var videoEntities = await query
            .OrderByDescending(v => v.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var videos = videoEntities.Select(v => new VideoSummaryDto
        {
            Id = v.Id,
            Title = v.Title,
            Description = v.Description,
            ThumbnailUrl = !string.IsNullOrEmpty(v.ThumbnailPath) ? $"{baseUrl}/api/stream/{v.Id}/thumbnail" : null,
            DurationMs = v.DurationMs,
            Status = v.Status.ToString(),
            CreatedAt = v.CreatedAt,
            CreatedByOid = v.CreatedByOid,
            LanguageHint = v.LanguageHint
        }).ToList();

        _logger.LogInformation("User {UserId} retrieved their {Count} videos (page {Page})",
            _currentUser.Id, videos.Count, page);

        return Ok(new VideoListResponse
        {
            Videos = videos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    /// <summary>
    /// Approve a video (Reviewer/Admin only)
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = Policies.CanReview)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApproveVideo(Guid id, CancellationToken cancellationToken)
    {
        var video = await _dbContext.VideoAssets.FindAsync(new object[] { id }, cancellationToken);

        if (video == null)
        {
            return NotFound();
        }

        // Kick off indexing if not already completed
        var searchJob = await _dbContext.VideoProcessingJobs
            .FirstOrDefaultAsync(j => j.VideoId == id && j.Stage == ProcessingStage.SearchIndexing, cancellationToken);

        if (searchJob == null)
        {
            searchJob = new VideoProcessingJob
            {
                Id = Guid.NewGuid(),
                VideoId = id,
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
            searchJob.Attempts = 0;
            searchJob.LastError = null;
            searchJob.StartedAt = null;
            searchJob.CompletedAt = null;
            searchJob.UpdatedAt = DateTime.UtcNow;
        }

        // Set video to Indexing until the search job completes; the job handler will publish.
        video.Status = VideoStatus.Indexing;
        video.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // enqueue index job if needed
        if (searchJob.Status != JobStatus.Completed)
        {
            await _jobQueue.EnqueueAsync(new IndexVideoJob
            {
                VideoAssetId = id,
                ProcessingJobId = searchJob.Id
            }, cancellationToken);
        }

        _logger.LogInformation("User {UserId} approved video {VideoId}", _currentUser.Id, id);

        return NoContent();
    }

    /// <summary>
    /// Reject a video (Reviewer/Admin only)
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = Policies.CanReview)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectVideo(
        Guid id,
        [FromBody] RejectVideoRequest request,
        CancellationToken cancellationToken)
    {
        var video = await _dbContext.VideoAssets.FindAsync(new object[] { id }, cancellationToken);

        if (video == null)
        {
            return NotFound();
        }

        video.Status = VideoStatus.Rejected;
        video.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} rejected video {VideoId}: {Reason}",
            _currentUser.Id, id, request.Reason);

        return NoContent();
    }

    /// <summary>
    /// Delete a video permanently (Admin only)
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = Policies.RequireAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteVideo(Guid id, CancellationToken cancellationToken)
    {
        var video = await _dbContext.VideoAssets
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        if (video == null)
        {
            return NotFound();
        }

        // Delete blobs from storage
        try
        {
            // Delete original video blob
            if (!string.IsNullOrEmpty(video.BlobPath))
            {
                await _blobStore.DeleteBlobAsync("videos", video.BlobPath, cancellationToken);
                _logger.LogInformation("Deleted original video blob: {BlobPath}", video.BlobPath);
            }

            // Delete thumbnail blob
            if (!string.IsNullOrEmpty(video.ThumbnailPath))
            {
                await _blobStore.DeleteBlobAsync("thumbnails", video.ThumbnailPath, cancellationToken);
                _logger.LogInformation("Deleted thumbnail blob: {BlobPath}", video.ThumbnailPath);
            }

            // Delete master playlist
            if (!string.IsNullOrEmpty(video.MasterPlaylistPath))
            {
                await _blobStore.DeleteBlobAsync("videos", video.MasterPlaylistPath, cancellationToken);
                _logger.LogInformation("Deleted master playlist: {Path}", video.MasterPlaylistPath);
            }

            // Delete HLS variants
            var variants = await _dbContext.VideoVariants
                .Where(v => v.VideoId == id)
                .ToListAsync(cancellationToken);

            foreach (var variant in variants)
            {
                // Delete playlist
                if (!string.IsNullOrEmpty(variant.PlaylistPath))
                {
                    await _blobStore.DeleteBlobAsync("videos", variant.PlaylistPath, cancellationToken);
                    _logger.LogInformation("Deleted variant playlist: {Path}", variant.PlaylistPath);
                }
                // Delete segments directory - note: segments are in a folder, may need special handling
                if (!string.IsNullOrEmpty(variant.SegmentsPath))
                {
                    // Try to delete the segments path as a blob prefix
                    _logger.LogInformation("Variant segments path to clean: {Path}", variant.SegmentsPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deleting blobs for video {VideoId}, continuing with database cleanup", id);
        }

        // Delete from database (cascade will handle related entities)
        _dbContext.VideoAssets.Remove(video);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} deleted video {VideoId}: {Title}",
            _currentUser.Id, id, video.Title);

        return NoContent();
    }

    /// <summary>
    /// Get the processing status of a video
    /// </summary>
    [HttpGet("{id:guid}/processing")]
    [Authorize(Policy = Policies.CanViewVideos)]
    [ProducesResponseType(typeof(ProcessingStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProcessingStatusResponse>> GetProcessingStatus(Guid id, CancellationToken cancellationToken)
    {
        var video = await _dbContext.VideoAssets.FindAsync(new object[] { id }, cancellationToken);

        if (video == null)
        {
            return NotFound();
        }

        // Security check: can user view this video?
        if (!CanUserViewVideo(video))
        {
            _logger.LogWarning("User {UserId} attempted to access processing status for video {VideoId} without permission",
                _currentUser.Id, id);
            return Forbid();
        }

        var processingJobs = await _dbContext.VideoProcessingJobs
            .Where(j => j.VideoId == id)
            .OrderBy(j => j.Stage)
            .Select(j => new ProcessingJobDto
            {
                Stage = j.Stage.ToString(),
                Status = j.Status.ToString(),
                Progress = j.Progress,
                ProgressMessage = j.ProgressMessage,
                Attempts = j.Attempts,
                LastError = j.LastError,
                StartedAt = j.StartedAt,
                CompletedAt = j.CompletedAt
            })
            .ToListAsync(cancellationToken);

        // Calculate overall progress
        var totalJobs = processingJobs.Count;
        var completedJobs = processingJobs.Count(j => j.Status == JobStatus.Completed.ToString() || j.Status == JobStatus.Skipped.ToString());
        var failedJobs = processingJobs.Count(j => j.Status == JobStatus.Failed.ToString());

        var overallStatus = failedJobs > 0 ? "Failed" :
            completedJobs == totalJobs && totalJobs > 0 ? "Completed" :
            processingJobs.Any(j => j.Status == JobStatus.InProgress.ToString()) ? "InProgress" :
            "Pending";

        // Get variant progress
        var variants = await _dbContext.VideoVariants
            .Where(v => v.VideoId == id)
            .OrderBy(v => v.Quality)
            .Select(v => new VariantProgressDto
            {
                Quality = v.Quality,
                Status = v.Status.ToString(),
                Progress = v.Progress,
                ProgressMessage = v.ProgressMessage
            })
            .ToListAsync(cancellationToken);

        var response = new ProcessingStatusResponse
        {
            VideoId = id,
            VideoStatus = video.Status.ToString(),
            OverallProcessingStatus = overallStatus,
            ProgressPercentage = totalJobs > 0 ? (int)((double)completedJobs / totalJobs * 100) : 0,
            Jobs = processingJobs,
            Variants = variants
        };

        return Ok(response);
    }

    /// <summary>
    /// Reprocess a video (re-enqueue for processing)
    /// </summary>
    [HttpPost("{id:guid}/reprocess")]
    [Authorize(Policy = Policies.CanUpload)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ReprocessVideo(Guid id, CancellationToken cancellationToken)
    {
        var video = await _dbContext.VideoAssets.FindAsync(new object[] { id }, cancellationToken);

        if (video == null)
        {
            return NotFound();
        }

        // Reset video status to Queued
        video.Status = VideoStatus.Queued;

        // Clear existing processing jobs
        var existingJobs = await _dbContext.VideoProcessingJobs
            .Where(j => j.VideoId == id)
            .ToListAsync(cancellationToken);
        _dbContext.VideoProcessingJobs.RemoveRange(existingJobs);

        // Clear existing variants
        var existingVariants = await _dbContext.Set<VideoVariant>()
            .Where(v => v.VideoId == id)
            .ToListAsync(cancellationToken);
        _dbContext.Set<VideoVariant>().RemoveRange(existingVariants);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Enqueue video processing job
        await _jobQueue.EnqueueAsync(new ProcessVideoJob
        {
            VideoAssetId = video.Id,
            BlobPath = video.BlobPath,
            LanguageHint = video.LanguageHint
        }, cancellationToken);

        _logger.LogInformation(
            "Video {VideoId} requeued for processing by user {UserId}",
            id, _currentUser.Id);

        return Ok(new { message = "Video requeued for processing" });
    }

    /// <summary>
    /// Regenerate AI highlights, summary, and tags for a video
    /// </summary>
    [HttpPost("{id:guid}/regenerate-ai")]
    [Authorize(Policy = Policies.CanUpload)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> RegenerateAI(Guid id, CancellationToken cancellationToken)
    {
        var video = await _dbContext.VideoAssets
            .Include(v => v.TranscriptSegments)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        if (video == null)
        {
            return NotFound();
        }

        if (!video.TranscriptSegments.Any())
        {
            return BadRequest(new { error = "Video has no transcript. Please wait for transcription to complete or trigger reprocessing." });
        }

        // Find or create an AI highlights job
        var aiJob = await _dbContext.VideoProcessingJobs
            .FirstOrDefaultAsync(j => j.VideoId == id && j.Stage == ProcessingStage.AIHighlights, cancellationToken);

        if (aiJob == null)
        {
            aiJob = new VideoProcessingJob
            {
                Id = Guid.NewGuid(),
                VideoId = id,
                Stage = ProcessingStage.AIHighlights,
                Status = JobStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.VideoProcessingJobs.Add(aiJob);
        }
        else
        {
            aiJob.Status = JobStatus.Pending;
            aiJob.Attempts = 0;
            aiJob.LastError = null;
            aiJob.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Enqueue the AI highlights job
        await _jobQueue.EnqueueAsync(new ExtractHighlightsJob
        {
            JobId = Guid.NewGuid(),
            VideoAssetId = id,
            ProcessingJobId = aiJob.Id
        }, cancellationToken);

        _logger.LogInformation(
            "AI highlights regeneration triggered for video {VideoId} by user {UserId}",
            id, _currentUser.Id);

        return Ok(new { message = "AI highlights regeneration queued" });
    }

    /// <summary>
    /// Get the transcript for a video
    /// </summary>
    [HttpGet("{id:guid}/transcript")]
    [Authorize(Policy = Policies.CanViewVideos)]
    [ProducesResponseType(typeof(TranscriptResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TranscriptResponse>> GetTranscript(Guid id, CancellationToken cancellationToken)
    {
        var video = await _dbContext.VideoAssets
            .Include(v => v.TranscriptSegments.OrderBy(t => t.StartMs))
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        if (video == null)
        {
            return NotFound();
        }

        // Security check: can user view this video?
        if (!CanUserViewVideo(video))
        {
            _logger.LogWarning("User {UserId} attempted to access transcript for video {VideoId} without permission",
                _currentUser.Id, id);
            return Forbid();
        }

        // Check if transcription is complete
        var transcriptionJob = await _dbContext.VideoProcessingJobs
            .FirstOrDefaultAsync(j => j.VideoId == id && j.Stage == ProcessingStage.Transcription, cancellationToken);

        var response = new TranscriptResponse
        {
            VideoId = id,
            TranscriptionStatus = transcriptionJob?.Status.ToString() ?? "NotStarted",
            DurationMs = video.DurationMs,
            Segments = video.TranscriptSegments.Select(t => new TranscriptSegmentDto
            {
                Id = t.Id,
                StartMs = t.StartMs,
                EndMs = t.EndMs,
                Text = t.Text,
                DetectedLanguage = t.DetectedLanguage,
                Speaker = t.Speaker,
                Confidence = t.Confidence
            }).ToList(),
            FullText = string.Join(" ", video.TranscriptSegments.Select(t => t.Text))
        };

        return Ok(response);
    }

    private bool CanUserViewVideo(VideoAsset video)
    {
        // Admins and reviewers can see everything
        if (_currentUser.IsInAnyRole(Roles.Admin, Roles.Reviewer))
            return true;

        // Users can see their own videos
        if (video.CreatedByOid == _currentUser.Id)
            return true;

        // Others can only see published videos
        return video.Status == VideoStatus.Published;
    }

    /// <summary>
    /// Get key highlights for a video with optional translation
    /// </summary>
    [HttpGet("{id:guid}/highlights")]
    [Authorize(Policy = Policies.CanViewVideos)]
    [ProducesResponseType(typeof(VideoHighlightsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<VideoHighlightsResponse>> GetHighlights(
        Guid id,
        [FromQuery] string? language = null,
        CancellationToken cancellationToken = default)
    {
        var video = await _dbContext.VideoAssets
            .Include(v => v.Highlights.OrderByDescending(h => h.Importance ?? 0))
                .ThenInclude(h => h.Translations)
            .Include(v => v.Summary)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        if (video == null)
        {
            return NotFound();
        }

        // Security check: can user view this video?
        if (!CanUserViewVideo(video))
        {
            return Forbid();
        }

        // Determine which language to return
        var targetLanguage = language ?? "en";

        var highlights = new List<HighlightWithLanguageDto>();
        foreach (var h in video.Highlights)
        {
            string displayText = h.Text; // English is default

            if (targetLanguage != "en")
            {
                // Check if we have a cached translation
                var existingTranslation = h.Translations?.FirstOrDefault(t => t.LanguageCode == targetLanguage);
                if (existingTranslation != null)
                {
                    displayText = existingTranslation.TranslatedText;
                }
                else if (!string.IsNullOrEmpty(h.OriginalText) && h.SourceLanguage == targetLanguage)
                {
                    // User wants original language and we have it
                    displayText = h.OriginalText;
                }
            }

            highlights.Add(new HighlightWithLanguageDto
            {
                Id = h.Id,
                Text = displayText,
                OriginalText = h.OriginalText,
                SourceLanguage = h.SourceLanguage,
                Category = h.Category,
                Importance = h.Importance,
                TimestampMs = h.TimestampMs,
                DisplayLanguage = targetLanguage
            });
        }

        var response = new VideoHighlightsResponse
        {
            VideoId = id,
            Highlights = highlights,
            AvailableLanguages = SupportedLanguages.GetAll().Select(l => new LanguageOptionDto
            {
                Code = l.Key,
                Name = l.Value
            }).ToList(),
            SourceLanguage = video.Highlights.FirstOrDefault()?.SourceLanguage ?? "en",
            CurrentLanguage = targetLanguage,
            Summary = video.Summary != null ? new SummaryWithLanguageDto
            {
                Summary = video.Summary.Summary,
                TlDr = video.Summary.TlDr,
                OriginalSummary = video.Summary.OriginalSummary,
                OriginalTlDr = video.Summary.OriginalTlDr,
                SourceLanguage = video.Summary.SourceLanguage
            } : null
        };

        return Ok(response);
    }

    /// <summary>
    /// Translate highlights to a specific language
    /// </summary>
    [HttpPost("{id:guid}/highlights/translate")]
    [Authorize(Policy = Policies.CanViewVideos)]
    [ProducesResponseType(typeof(TranslateHighlightsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TranslateHighlightsResponse>> TranslateHighlights(
        Guid id,
        [FromBody] TranslateHighlightsRequest request,
        CancellationToken cancellationToken)
    {
        if (!SupportedLanguages.IsSupported(request.TargetLanguage))
        {
            return BadRequest(new { error = $"Language '{request.TargetLanguage}' is not supported. Supported languages: {string.Join(", ", SupportedLanguages.GetAll().Keys)}" });
        }

        var video = await _dbContext.VideoAssets
            .Include(v => v.Highlights)
                .ThenInclude(h => h.Translations)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        if (video == null)
        {
            return NotFound();
        }

        // Security check
        if (!CanUserViewVideo(video))
        {
            return Forbid();
        }

        if (!video.Highlights.Any())
        {
            return BadRequest(new { error = "Video has no highlights to translate" });
        }

        var translatedHighlights = new List<TranslatedHighlightDto>();

        foreach (var highlight in video.Highlights)
        {
            string translatedText;

            // Check if translation already exists
            var existingTranslation = highlight.Translations?.FirstOrDefault(t => t.LanguageCode == request.TargetLanguage);
            if (existingTranslation != null)
            {
                translatedText = existingTranslation.TranslatedText;
            }
            else if (request.TargetLanguage == "en")
            {
                // English is the default stored language
                translatedText = highlight.Text;
            }
            else if (!string.IsNullOrEmpty(highlight.OriginalText) && highlight.SourceLanguage == request.TargetLanguage)
            {
                // User wants original language
                translatedText = highlight.OriginalText;
            }
            else
            {
                // Need to translate from English
                try
                {
                    var translationResult = await _translationService.TranslateFromEnglishAsync(
                        highlight.Text,
                        request.TargetLanguage,
                        cancellationToken);

                    if (translationResult.Success && !string.IsNullOrEmpty(translationResult.TranslatedText))
                    {
                        translatedText = translationResult.TranslatedText;

                        // Cache the translation
                        var newTranslation = new HighlightTranslation
                        {
                            Id = Guid.NewGuid(),
                            VideoHighlightId = highlight.Id,
                            LanguageCode = request.TargetLanguage,
                            TranslatedText = translatedText,
                            CreatedAt = DateTime.UtcNow
                        };
                        _dbContext.HighlightTranslations.Add(newTranslation);
                    }
                    else
                    {
                        _logger.LogWarning("Translation failed for highlight {HighlightId}: {Error}", highlight.Id, translationResult.Error);
                        translatedText = highlight.Text; // Fallback to English
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to translate highlight {HighlightId} to {Language}", highlight.Id, request.TargetLanguage);
                    translatedText = highlight.Text; // Fallback to English
                }
            }

            translatedHighlights.Add(new TranslatedHighlightDto
            {
                Id = highlight.Id,
                OriginalText = highlight.Text,
                TranslatedText = translatedText,
                Category = highlight.Category,
                Importance = highlight.Importance,
                TimestampMs = highlight.TimestampMs
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Translated {Count} highlights to {Language} for video {VideoId}",
            translatedHighlights.Count, request.TargetLanguage, id);

        return Ok(new TranslateHighlightsResponse
        {
            VideoId = id,
            TargetLanguage = request.TargetLanguage,
            TargetLanguageName = SupportedLanguages.GetName(request.TargetLanguage),
            Highlights = translatedHighlights
        });
    }

    /// <summary>
    /// Get list of supported languages for translation
    /// </summary>
    [HttpGet("supported-languages")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SupportedLanguagesResponse), StatusCodes.Status200OK)]
    public ActionResult<SupportedLanguagesResponse> GetSupportedLanguages()
    {
        return Ok(new SupportedLanguagesResponse
        {
            Languages = SupportedLanguages.GetAll().Select(l => new LanguageOptionDto
            {
                Code = l.Key,
                Name = l.Value
            }).ToList()
        });
    }
}

// DTOs
public record VideoListResponse
{
    public required List<VideoSummaryDto> Videos { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

public record VideoSummaryDto
{
    public Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? ThumbnailUrl { get; init; }
    public long? DurationMs { get; init; }
    public required string Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public required string CreatedByOid { get; init; }
    public string? LanguageHint { get; init; }
}

public record VideoDetailDto : VideoSummaryDto
{
    public required string BlobPath { get; init; }
    public required List<TranscriptSegmentDto> TranscriptSegments { get; init; }
    public List<VideoHighlightDto>? Highlights { get; init; }
    public VideoSummaryInfoDto? SummaryInfo { get; init; }
    public string[]? Tags { get; init; }
}

public record VideoHighlightDto
{
    public Guid Id { get; init; }
    public required string Text { get; init; }
    public required string Category { get; init; }
    public float? Importance { get; init; }
    public long? TimestampMs { get; init; }
}

public record HighlightWithLanguageDto : VideoHighlightDto
{
    public string? OriginalText { get; init; }
    public string? SourceLanguage { get; init; }
    public string? DisplayLanguage { get; init; }
}

public record VideoHighlightsResponse
{
    public Guid VideoId { get; init; }
    public required List<HighlightWithLanguageDto> Highlights { get; init; }
    public required List<LanguageOptionDto> AvailableLanguages { get; init; }
    public string? SourceLanguage { get; init; }
    public required string CurrentLanguage { get; init; }
    public SummaryWithLanguageDto? Summary { get; init; }
}

public record SummaryWithLanguageDto
{
    public string? Summary { get; init; }
    public string? TlDr { get; init; }
    public string? OriginalSummary { get; init; }
    public string? OriginalTlDr { get; init; }
    public string? SourceLanguage { get; init; }
}

public record LanguageOptionDto
{
    public required string Code { get; init; }
    public required string Name { get; init; }
}

public record TranslateHighlightsRequest
{
    public required string TargetLanguage { get; init; }
}

public record TranslateHighlightsResponse
{
    public Guid VideoId { get; init; }
    public required string TargetLanguage { get; init; }
    public required string TargetLanguageName { get; init; }
    public required List<TranslatedHighlightDto> Highlights { get; init; }
}

public record TranslatedHighlightDto
{
    public Guid Id { get; init; }
    public required string OriginalText { get; init; }
    public required string TranslatedText { get; init; }
    public required string Category { get; init; }
    public float? Importance { get; init; }
    public long? TimestampMs { get; init; }
}

public record SupportedLanguagesResponse
{
    public required List<LanguageOptionDto> Languages { get; init; }
}

public record VideoSummaryInfoDto
{
    public string? Summary { get; init; }
    public string? TlDr { get; init; }
    public string[]? Keywords { get; init; }
    public string[]? Topics { get; init; }
    public string? Sentiment { get; init; }
}

public record TranscriptSegmentDto
{
    public Guid Id { get; init; }
    public long StartMs { get; init; }
    public long EndMs { get; init; }
    public required string Text { get; init; }
    public string? DetectedLanguage { get; init; }
    public string? Speaker { get; init; }
    public double? Confidence { get; init; }
}

public record RejectVideoRequest
{
    public required string Reason { get; init; }
}

public record ProcessingStatusResponse
{
    public Guid VideoId { get; init; }
    public required string VideoStatus { get; init; }
    public required string OverallProcessingStatus { get; init; }
    public int ProgressPercentage { get; init; }
    public required List<ProcessingJobDto> Jobs { get; init; }
    public List<VariantProgressDto>? Variants { get; init; }
}

public record VariantProgressDto
{
    public required string Quality { get; init; }
    public required string Status { get; init; }
    public int Progress { get; init; }
    public string? ProgressMessage { get; init; }
}

public record ProcessingJobDto
{
    public required string Stage { get; init; }
    public required string Status { get; init; }
    public int Progress { get; init; } // 0-100 percentage
    public string? ProgressMessage { get; init; }
    public int Attempts { get; init; }
    public string? LastError { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}

public record TranscriptResponse
{
    public Guid VideoId { get; init; }
    public required string TranscriptionStatus { get; init; }
    public long? DurationMs { get; init; }
    public required List<TranscriptSegmentDto> Segments { get; init; }
    public required string FullText { get; init; }
}
