using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using T4L.VideoSearch.Api.Auth;
using T4L.VideoSearch.Api.Domain.Entities;
using T4L.VideoSearch.Api.Infrastructure.Persistence;

namespace T4L.VideoSearch.Api.Controllers;

/// <summary>
/// Content moderation endpoints for reviewers and administrators
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.CanReview)]
public class ModerationController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<ModerationController> _logger;

    public ModerationController(
        AppDbContext dbContext,
        ICurrentUser currentUser,
        ILogger<ModerationController> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Get the moderation queue - videos pending review
    /// </summary>
    [HttpGet("queue")]
    [ProducesResponseType(typeof(ModerationQueueResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ModerationQueueResponse>> GetModerationQueue(
        [FromQuery] string? severity = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.VideoAssets
            .Include(v => v.ModerationResult)
            .Where(v => v.Status == VideoStatus.Quarantined || v.Status == VideoStatus.Moderating);

        // Filter by severity if specified
        if (!string.IsNullOrEmpty(severity) && Enum.TryParse<ModerationSeverity>(severity, true, out var severityFilter))
        {
            query = query.Where(v => v.ModerationResult != null && v.ModerationResult.HighestSeverity == severityFilter);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var videos = await query
            .OrderByDescending(v => v.ModerationResult != null ? v.ModerationResult.HighestSeverity : null)
            .ThenBy(v => v.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new ModerationQueueItemDto
            {
                VideoId = v.Id,
                Title = v.Title,
                Description = v.Description,
                ThumbnailUrl = v.ThumbnailPath,
                DurationMs = v.DurationMs,
                Status = v.Status.ToString(),
                CreatedAt = v.CreatedAt,
                CreatedByOid = v.CreatedByOid,
                ModerationResult = v.ModerationResult != null ? new ModerationResultDto
                {
                    ContentSafetyStatus = v.ModerationResult.ContentSafetyStatus.ToString(),
                    MalwareScanStatus = v.ModerationResult.MalwareScanStatus.ToString(),
                    Severity = v.ModerationResult.HighestSeverity != null
                        ? v.ModerationResult.HighestSeverity.Value.ToString()
                        : null,
                    Reasons = v.ModerationResult.Reasons,
                    CreatedAt = v.ModerationResult.CreatedAt
                } : null
            })
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Moderator {UserId} retrieved moderation queue ({Count} items)",
            _currentUser.Id, videos.Count);

        return Ok(new ModerationQueueResponse
        {
            Items = videos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    /// <summary>
    /// Get detailed moderation info for a specific video
    /// </summary>
    [HttpGet("{videoId:guid}")]
    [ProducesResponseType(typeof(ModerationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ModerationDetailDto>> GetModerationDetail(
        Guid videoId,
        CancellationToken cancellationToken)
    {
        var video = await _dbContext.VideoAssets
            .Include(v => v.ModerationResult)
            .Include(v => v.TranscriptSegments.OrderBy(t => t.StartMs))
            .FirstOrDefaultAsync(v => v.Id == videoId, cancellationToken);

        if (video == null)
        {
            return NotFound();
        }

        var detail = new ModerationDetailDto
        {
            VideoId = video.Id,
            Title = video.Title,
            Description = video.Description,
            ThumbnailUrl = video.ThumbnailPath,
            BlobPath = video.BlobPath,
            DurationMs = video.DurationMs,
            Status = video.Status.ToString(),
            CreatedAt = video.CreatedAt,
            CreatedByOid = video.CreatedByOid,
            ModerationResult = video.ModerationResult != null ? new ModerationResultDetailDto
            {
                Id = video.ModerationResult.Id,
                ContentSafetyStatus = video.ModerationResult.ContentSafetyStatus.ToString(),
                MalwareScanStatus = video.ModerationResult.MalwareScanStatus.ToString(),
                Severity = video.ModerationResult.HighestSeverity?.ToString(),
                Reasons = video.ModerationResult.Reasons,
                ReviewerDecision = video.ModerationResult.ReviewerDecision?.ToString(),
                ReviewerOid = video.ModerationResult.ReviewerOid,
                ReviewerNotes = video.ModerationResult.ReviewerNotes,
                ReviewedAt = video.ModerationResult.ReviewedAt,
                CreatedAt = video.ModerationResult.CreatedAt
            } : null,
            TranscriptPreview = video.TranscriptSegments
                .Take(20)
                .Select(t => new TranscriptPreviewDto
                {
                    StartMs = t.StartMs,
                    EndMs = t.EndMs,
                    Text = t.Text
                })
                .ToList()
        };

        _logger.LogInformation("Moderator {UserId} viewed moderation detail for video {VideoId}",
            _currentUser.Id, videoId);

        return Ok(detail);
    }

    /// <summary>
    /// Approve a video in moderation
    /// </summary>
    [HttpPost("{videoId:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApproveVideo(
        Guid videoId,
        [FromBody] ModerationDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        var video = await _dbContext.VideoAssets
            .Include(v => v.ModerationResult)
            .FirstOrDefaultAsync(v => v.Id == videoId, cancellationToken);

        if (video == null)
        {
            return NotFound();
        }

        if (video.Status != VideoStatus.Quarantined && video.Status != VideoStatus.Moderating)
        {
            return BadRequest("Video is not pending moderation review");
        }

        // Update moderation result
        if (video.ModerationResult != null)
        {
            video.ModerationResult.ReviewerDecision = ReviewerDecision.Approved;
            video.ModerationResult.ReviewerOid = _currentUser.Id;
            video.ModerationResult.ReviewerNotes = request?.Notes;
            video.ModerationResult.ReviewedAt = DateTime.UtcNow;
        }

        // Move video to Indexing status (will be published after indexing)
        video.Status = VideoStatus.Indexing;
        video.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Moderator {UserId} approved video {VideoId}",
            _currentUser.Id, videoId);

        return NoContent();
    }

    /// <summary>
    /// Reject a video in moderation
    /// </summary>
    [HttpPost("{videoId:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectVideo(
        Guid videoId,
        [FromBody] ModerationDecisionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Notes))
        {
            return BadRequest("Rejection reason is required");
        }

        var video = await _dbContext.VideoAssets
            .Include(v => v.ModerationResult)
            .FirstOrDefaultAsync(v => v.Id == videoId, cancellationToken);

        if (video == null)
        {
            return NotFound();
        }

        if (video.Status != VideoStatus.Quarantined && video.Status != VideoStatus.Moderating)
        {
            return BadRequest("Video is not pending moderation review");
        }

        // Update moderation result
        if (video.ModerationResult != null)
        {
            video.ModerationResult.ReviewerDecision = ReviewerDecision.Rejected;
            video.ModerationResult.ReviewerOid = _currentUser.Id;
            video.ModerationResult.ReviewerNotes = request.Notes;
            video.ModerationResult.ReviewedAt = DateTime.UtcNow;
        }

        // Update video status to Rejected
        video.Status = VideoStatus.Rejected;
        video.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Moderator {UserId} rejected video {VideoId}: {Reason}",
            _currentUser.Id, videoId, request.Notes);

        return NoContent();
    }

    /// <summary>
    /// Get moderation statistics
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ModerationStatsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ModerationStatsResponse>> GetModerationStats(CancellationToken cancellationToken)
    {
        var pendingCount = await _dbContext.VideoAssets
            .CountAsync(v => v.Status == VideoStatus.Quarantined || v.Status == VideoStatus.Moderating, cancellationToken);

        var last24Hours = DateTime.UtcNow.AddHours(-24);
        var reviewedLast24h = await _dbContext.ModerationResults
            .CountAsync(m => m.ReviewedAt != null && m.ReviewedAt > last24Hours, cancellationToken);

        var severityCounts = await _dbContext.VideoAssets
            .Where(v => v.Status == VideoStatus.Quarantined || v.Status == VideoStatus.Moderating)
            .Where(v => v.ModerationResult != null && v.ModerationResult.HighestSeverity != null)
            .GroupBy(v => v.ModerationResult!.HighestSeverity)
            .Select(g => new { Severity = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var stats = new ModerationStatsResponse
        {
            PendingReviewCount = pendingCount,
            ReviewedLast24Hours = reviewedLast24h,
            HighSeverityCount = severityCounts.FirstOrDefault(s => s.Severity == ModerationSeverity.High)?.Count ?? 0,
            MediumSeverityCount = severityCounts.FirstOrDefault(s => s.Severity == ModerationSeverity.Medium)?.Count ?? 0,
            LowSeverityCount = severityCounts.FirstOrDefault(s => s.Severity == ModerationSeverity.Low)?.Count ?? 0
        };

        return Ok(stats);
    }

    /// <summary>
    /// Get moderation history/audit log
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(ModerationHistoryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ModerationHistoryResponse>> GetModerationHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ModerationResults
            .Include(m => m.Video)
            .Where(m => m.ReviewedAt != null)
            .OrderByDescending(m => m.ReviewedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new ModerationHistoryItemDto
            {
                VideoId = m.VideoId,
                VideoTitle = m.Video.Title,
                Decision = m.ReviewerDecision!.ToString()!,
                ReviewerOid = m.ReviewerOid!,
                ReviewerNotes = m.ReviewerNotes,
                Severity = m.HighestSeverity != null ? m.HighestSeverity.Value.ToString() : null,
                Reasons = m.Reasons,
                ReviewedAt = m.ReviewedAt!.Value
            })
            .ToListAsync(cancellationToken);

        return Ok(new ModerationHistoryResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }
}

// DTOs
public record ModerationQueueResponse
{
    public required List<ModerationQueueItemDto> Items { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
}

public record ModerationQueueItemDto
{
    public Guid VideoId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? ThumbnailUrl { get; init; }
    public long? DurationMs { get; init; }
    public required string Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public required string CreatedByOid { get; init; }
    public ModerationResultDto? ModerationResult { get; init; }
}

public record ModerationResultDto
{
    public required string ContentSafetyStatus { get; init; }
    public required string MalwareScanStatus { get; init; }
    public string? Severity { get; init; }
    public string[] Reasons { get; init; } = [];
    public DateTime CreatedAt { get; init; }
}

public record ModerationDetailDto
{
    public Guid VideoId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? ThumbnailUrl { get; init; }
    public required string BlobPath { get; init; }
    public long? DurationMs { get; init; }
    public required string Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public required string CreatedByOid { get; init; }
    public ModerationResultDetailDto? ModerationResult { get; init; }
    public required List<TranscriptPreviewDto> TranscriptPreview { get; init; }
}

public record ModerationResultDetailDto : ModerationResultDto
{
    public Guid Id { get; init; }
    public string? ReviewerDecision { get; init; }
    public string? ReviewerOid { get; init; }
    public string? ReviewerNotes { get; init; }
    public DateTime? ReviewedAt { get; init; }
}

public record TranscriptPreviewDto
{
    public long StartMs { get; init; }
    public long EndMs { get; init; }
    public required string Text { get; init; }
}

public record ModerationDecisionRequest
{
    public string? Notes { get; init; }
}

public record ModerationStatsResponse
{
    public int PendingReviewCount { get; init; }
    public int ReviewedLast24Hours { get; init; }
    public int HighSeverityCount { get; init; }
    public int MediumSeverityCount { get; init; }
    public int LowSeverityCount { get; init; }
}

public record ModerationHistoryResponse
{
    public required List<ModerationHistoryItemDto> Items { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public record ModerationHistoryItemDto
{
    public Guid VideoId { get; init; }
    public required string VideoTitle { get; init; }
    public required string Decision { get; init; }
    public required string ReviewerOid { get; init; }
    public string? ReviewerNotes { get; init; }
    public string? Severity { get; init; }
    public string[] Reasons { get; init; } = [];
    public DateTime ReviewedAt { get; init; }
}
