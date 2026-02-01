using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using T4L.VideoSearch.Api.Auth;
using T4L.VideoSearch.Api.Domain.Entities;
using T4L.VideoSearch.Api.Infrastructure.Adapters;
using T4L.VideoSearch.Api.Infrastructure.BackgroundJobs;
using T4L.VideoSearch.Api.Infrastructure.BackgroundJobs.Jobs;
using T4L.VideoSearch.Api.Infrastructure.Persistence;

namespace T4L.VideoSearch.Api.Controllers;

/// <summary>
/// Handles video uploads with chunked upload support for large files
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.CanUpload)]
public class UploadController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IChunkedBlobStore _blobStore;
    private readonly IJobQueue _jobQueue;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<UploadController> _logger;
    private readonly IConfiguration _configuration;

    private const string UploadContainer = "uploads";
    private const string VideosContainer = "videos";
    private const long DefaultChunkSize = 4 * 1024 * 1024; // 4MB
    private const long MaxFileSize = 5L * 1024 * 1024 * 1024; // 5GB
    private static readonly string[] AllowedContentTypes = [
        "video/mp4",
        "video/quicktime",
        "video/x-msvideo",
        "video/x-matroska",
        "video/webm",
        "video/mpeg"
    ];

    public UploadController(
        AppDbContext dbContext,
        IChunkedBlobStore blobStore,
        IJobQueue jobQueue,
        ICurrentUser currentUser,
        IConfiguration configuration,
        ILogger<UploadController> logger)
    {
        _dbContext = dbContext;
        _blobStore = blobStore;
        _jobQueue = jobQueue;
        _currentUser = currentUser;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Initialize a new upload session for chunked upload
    /// </summary>
    [HttpPost("sessions")]
    [ProducesResponseType(typeof(UploadSessionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UploadSessionResponse>> CreateUploadSession(
        [FromBody] CreateUploadSessionRequest request,
        CancellationToken cancellationToken)
    {
        // Validate file size
        if (request.FileSize <= 0 || request.FileSize > MaxFileSize)
        {
            return BadRequest(new { error = $"File size must be between 1 byte and {MaxFileSize / (1024 * 1024 * 1024)}GB" });
        }

        // Validate content type
        if (!AllowedContentTypes.Contains(request.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = $"Content type '{request.ContentType}' is not allowed. Allowed types: {string.Join(", ", AllowedContentTypes)}" });
        }

        // Calculate chunks
        var chunkSize = request.ChunkSize > 0 ? request.ChunkSize : DefaultChunkSize;
        chunkSize = Math.Min(chunkSize, 100 * 1024 * 1024); // Max 100MB per chunk
        var totalChunks = (int)Math.Ceiling((double)request.FileSize / chunkSize);

        // Prevent duplicate uploads (same user, file name, size, still active)
        var existingActive = await _dbContext.UploadSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.CreatedByOid == _currentUser.Id &&
                s.FileName == request.FileName &&
                s.FileSize == request.FileSize &&
                s.Status != UploadSessionStatus.Completed &&
                s.Status != UploadSessionStatus.Failed &&
                s.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

        if (existingActive != null)
        {
            return Ok(new UploadSessionResponse
            {
                SessionId = existingActive.Id,
                FileName = existingActive.FileName,
                FileSize = existingActive.FileSize,
                ChunkSize = existingActive.ChunkSize,
                TotalChunks = existingActive.TotalChunks,
                UploadedChunks = existingActive.UploadedChunks,
                Status = existingActive.Status.ToString(),
                ExpiresAt = existingActive.ExpiresAt,
                BlobPath = existingActive.BlobPath,
                VideoAssetId = existingActive.VideoAssetId
            });
        }

        var existingCompleted = await _dbContext.UploadSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.CreatedByOid == _currentUser.Id &&
                s.FileName == request.FileName &&
                s.FileSize == request.FileSize &&
                s.Status == UploadSessionStatus.Completed,
                cancellationToken);

        // Completed uploads should not block new uploads; start a fresh session even if same file/size

        // Generate unique blob path
        var sessionId = Guid.NewGuid();
        var fileExtension = Path.GetExtension(request.FileName);
        var blobName = $"{_currentUser.Id}/{sessionId}{fileExtension}";

        // Create session
        var session = new UploadSession
        {
            Id = sessionId,
            TenantId = null, // TODO: Get from claims for multi-tenant
            FileName = SanitizeFileName(request.FileName),
            ContentType = request.ContentType,
            FileSize = request.FileSize,
            ChunkSize = chunkSize,
            TotalChunks = totalChunks,
            UploadedChunks = 0,
            Status = UploadSessionStatus.Created,
            BlobPath = $"{UploadContainer}/{blobName}",
            CreatedByOid = _currentUser.Id ?? throw new InvalidOperationException("User ID not found"),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24), // Sessions expire after 24 hours
            Title = request.Title,
            Description = request.Description,
            Tags = request.Tags ?? [],
            LanguageHint = request.LanguageHint,
            BlockIds = new string[totalChunks]
        };

        _dbContext.UploadSessions.Add(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created upload session {SessionId} for user {UserId}: {FileName} ({FileSize} bytes, {TotalChunks} chunks)",
            session.Id, _currentUser.Id, session.FileName, session.FileSize, session.TotalChunks);

        return CreatedAtAction(nameof(GetUploadSession), new { sessionId = session.Id }, new UploadSessionResponse
        {
            SessionId = session.Id,
            FileName = session.FileName,
            FileSize = session.FileSize,
            ChunkSize = session.ChunkSize,
            TotalChunks = session.TotalChunks,
            UploadedChunks = 0,
            Status = session.Status.ToString(),
            ExpiresAt = session.ExpiresAt,
            BlobPath = session.BlobPath
        });
    }

    /// <summary>
    /// Get upload session status
    /// </summary>
    [HttpGet("sessions/{sessionId:guid}")]
    [ProducesResponseType(typeof(UploadSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UploadSessionResponse>> GetUploadSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _dbContext.UploadSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.CreatedByOid == _currentUser.Id, cancellationToken);

        if (session == null)
        {
            return NotFound();
        }

        return Ok(new UploadSessionResponse
        {
            SessionId = session.Id,
            FileName = session.FileName,
            FileSize = session.FileSize,
            ChunkSize = session.ChunkSize,
            TotalChunks = session.TotalChunks,
            UploadedChunks = session.UploadedChunks,
            Status = session.Status.ToString(),
            ExpiresAt = session.ExpiresAt,
            BlobPath = session.BlobPath,
            VideoAssetId = session.VideoAssetId
        });
    }

    /// <summary>
    /// Get a signed URL for uploading a specific chunk
    /// </summary>
    [HttpGet("sessions/{sessionId:guid}/chunks/{chunkIndex:int}/url")]
    [ProducesResponseType(typeof(ChunkUploadUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChunkUploadUrlResponse>> GetChunkUploadUrl(
        Guid sessionId,
        int chunkIndex,
        CancellationToken cancellationToken)
    {
        var session = await _dbContext.UploadSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.CreatedByOid == _currentUser.Id, cancellationToken);

        if (session == null)
        {
            return NotFound();
        }

        if (session.Status != UploadSessionStatus.Created && session.Status != UploadSessionStatus.Uploading)
        {
            return BadRequest(new { error = $"Session is in invalid state: {session.Status}" });
        }

        if (chunkIndex < 0 || chunkIndex >= session.TotalChunks)
        {
            return BadRequest(new { error = $"Chunk index must be between 0 and {session.TotalChunks - 1}" });
        }

        if (session.ExpiresAt < DateTime.UtcNow)
        {
            return BadRequest(new { error = "Upload session has expired" });
        }

        // Generate block ID (must be base64 encoded and consistent)
        var blockId = GenerateBlockId(chunkIndex);

        // Extract container and blob name from path
        var pathParts = session.BlobPath.Split('/', 2);
        var containerName = pathParts[0];
        var blobName = pathParts[1];

        // Generate chunk upload URL
        var chunkUrl = await _blobStore.GenerateChunkUploadUrlAsync(
            containerName,
            blobName,
            blockId,
            TimeSpan.FromMinutes(30),
            cancellationToken);

        return Ok(new ChunkUploadUrlResponse
        {
            Url = chunkUrl.Url,
            BlockId = chunkUrl.BlockId,
            ExpiresAt = chunkUrl.ExpiresAt,
            ChunkIndex = chunkIndex,
            ChunkSize = chunkIndex == session.TotalChunks - 1
                ? session.FileSize - (session.ChunkSize * (session.TotalChunks - 1)) // Last chunk may be smaller
                : session.ChunkSize,
            RequiredHeaders = chunkUrl.RequiredHeaders
        });
    }

    /// <summary>
    /// Report that a chunk has been uploaded successfully
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/chunks/{chunkIndex:int}/complete")]
    [ProducesResponseType(typeof(ChunkCompleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChunkCompleteResponse>> CompleteChunk(
        Guid sessionId,
        int chunkIndex,
        CancellationToken cancellationToken)
    {
        var session = await _dbContext.UploadSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.CreatedByOid == _currentUser.Id, cancellationToken);

        if (session == null)
        {
            return NotFound();
        }

        if (chunkIndex < 0 || chunkIndex >= session.TotalChunks)
        {
            return BadRequest(new { error = $"Chunk index must be between 0 and {session.TotalChunks - 1}" });
        }

        // Update session with the block ID
        var blockId = GenerateBlockId(chunkIndex);
        var blockIds = session.BlockIds.ToList();

        if (string.IsNullOrEmpty(blockIds[chunkIndex]))
        {
            blockIds[chunkIndex] = blockId;
            session.BlockIds = blockIds.ToArray();
            session.UploadedChunks = blockIds.Count(b => !string.IsNullOrEmpty(b));
            session.Status = UploadSessionStatus.Uploading;

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogDebug(
                "Chunk {ChunkIndex}/{TotalChunks} completed for session {SessionId}",
                chunkIndex + 1, session.TotalChunks, sessionId);
        }

        return Ok(new ChunkCompleteResponse
        {
            ChunkIndex = chunkIndex,
            UploadedChunks = session.UploadedChunks,
            TotalChunks = session.TotalChunks,
            IsComplete = session.UploadedChunks == session.TotalChunks
        });
    }

    /// <summary>
    /// Upload a chunk directly through the API (alternative to direct blob upload)
    /// </summary>
    [HttpPut("sessions/{sessionId:guid}/chunks/{chunkIndex:int}")]
    [RequestSizeLimit(105 * 1024 * 1024)] // 100MB + overhead
    [ProducesResponseType(typeof(ChunkCompleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChunkCompleteResponse>> UploadChunk(
        Guid sessionId,
        int chunkIndex,
        CancellationToken cancellationToken)
    {
        // First validate the session exists and user has access (without tracking to avoid conflicts)
        var session = await _dbContext.UploadSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.CreatedByOid == _currentUser.Id, cancellationToken);

        if (session == null)
        {
            return NotFound();
        }

        if (session.Status != UploadSessionStatus.Created && session.Status != UploadSessionStatus.Uploading)
        {
            return BadRequest(new { error = $"Session is in invalid state: {session.Status}" });
        }

        if (chunkIndex < 0 || chunkIndex >= session.TotalChunks)
        {
            return BadRequest(new { error = $"Chunk index must be between 0 and {session.TotalChunks - 1}" });
        }

        // Generate block ID
        var blockId = GenerateBlockId(chunkIndex);

        // Extract container and blob name
        var pathParts = session.BlobPath.Split('/', 2);
        var containerName = pathParts[0];
        var blobName = pathParts[1];

        // Copy request body to a MemoryStream (Azure SDK requires seekable stream)
        using var memoryStream = new MemoryStream();
        await Request.Body.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        // Stage the block in blob storage
        await _blobStore.StageBlockAsync(containerName, blobName, blockId, memoryStream, cancellationToken);

        // Atomically update the BlockIds array using raw SQL to handle concurrent uploads
        // PostgreSQL array update: SET block_ids[index] = value (1-indexed in PostgreSQL)
        var pgIndex = chunkIndex + 1; // PostgreSQL arrays are 1-indexed
        await _dbContext.Database.ExecuteSqlRawAsync(
            @"UPDATE upload_sessions
              SET block_ids[{0}] = {1},
                  status = {2}
              WHERE id = {3}",
            pgIndex, blockId, "Uploading", sessionId);

        // Re-fetch to get updated state
        var updatedSession = await _dbContext.UploadSessions
            .AsNoTracking()
            .FirstAsync(s => s.Id == sessionId, cancellationToken);

        var uploadedChunks = updatedSession.BlockIds.Count(b => !string.IsNullOrEmpty(b));

        _logger.LogDebug(
            "Chunk {ChunkIndex}/{TotalChunks} uploaded for session {SessionId}",
            chunkIndex + 1, session.TotalChunks, sessionId);

        return Ok(new ChunkCompleteResponse
        {
            ChunkIndex = chunkIndex,
            UploadedChunks = uploadedChunks,
            TotalChunks = session.TotalChunks,
            IsComplete = uploadedChunks == session.TotalChunks
        });
    }

    /// <summary>
    /// Complete the upload session and create the video asset
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/complete")]
    [ProducesResponseType(typeof(UploadCompleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UploadCompleteResponse>> CompleteUpload(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await _dbContext.UploadSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.CreatedByOid == _currentUser.Id, cancellationToken);

        if (session == null)
        {
            return NotFound();
        }

        if (session.Status == UploadSessionStatus.Completed)
        {
            return Ok(new UploadCompleteResponse
            {
                SessionId = session.Id,
                VideoAssetId = session.VideoAssetId!.Value,
                Status = "Completed"
            });
        }

        // Recalculate uploaded chunks count from BlockIds array (handles concurrent upload race conditions)
        var actualUploadedChunks = session.BlockIds.Count(b => !string.IsNullOrEmpty(b));
        if (actualUploadedChunks != session.TotalChunks)
        {
            return BadRequest(new { error = $"Upload incomplete: {actualUploadedChunks}/{session.TotalChunks} chunks uploaded" });
        }

        try
        {
            session.Status = UploadSessionStatus.Completing;
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Extract container and blob name
            var pathParts = session.BlobPath.Split('/', 2);
            var containerName = pathParts[0];
            var blobName = pathParts[1];

            // Commit all blocks
            var validBlockIds = session.BlockIds.Where(b => !string.IsNullOrEmpty(b)).ToList();
            await _blobStore.CommitBlocksAsync(containerName, blobName, validBlockIds, session.ContentType, cancellationToken);

            // Move blob to videos container
            var videoBlobName = $"{_currentUser.Id}/{session.Id}{Path.GetExtension(session.FileName)}";
            await _blobStore.MoveBlobAsync(containerName, blobName, VideosContainer, videoBlobName, cancellationToken);

            // Create video asset
            var videoAsset = new VideoAsset
            {
                Id = Guid.NewGuid(),
                TenantId = session.TenantId,
                Title = session.Title,
                Description = session.Description,
                Tags = session.Tags,
                LanguageHint = session.LanguageHint,
                Status = VideoStatus.Queued, // Ready for processing
                BlobPath = $"{VideosContainer}/{videoBlobName}",
                CreatedByOid = session.CreatedByOid,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.VideoAssets.Add(videoAsset);

            // Update session
            session.Status = UploadSessionStatus.Completed;
            session.CompletedAt = DateTime.UtcNow;
            session.VideoAssetId = videoAsset.Id;

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Enqueue video processing job
            await _jobQueue.EnqueueAsync(new ProcessVideoJob
            {
                VideoAssetId = videoAsset.Id,
                BlobPath = videoAsset.BlobPath,
                LanguageHint = videoAsset.LanguageHint
            }, cancellationToken);

            _logger.LogInformation(
                "Upload session {SessionId} completed. Created video asset {VideoAssetId} and enqueued for processing",
                session.Id, videoAsset.Id);

            return Ok(new UploadCompleteResponse
            {
                SessionId = session.Id,
                VideoAssetId = videoAsset.Id,
                Status = "Completed"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete upload session {SessionId}", sessionId);

            session.Status = UploadSessionStatus.Failed;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return StatusCode(500, new { error = "Failed to complete upload" });
        }
    }

    /// <summary>
    /// Cancel an upload session
    /// </summary>
    [HttpDelete("sessions/{sessionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelUpload(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _dbContext.UploadSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.CreatedByOid == _currentUser.Id, cancellationToken);

        if (session == null)
        {
            return NotFound();
        }

        // Try to delete any uploaded blobs
        try
        {
            var pathParts = session.BlobPath.Split('/', 2);
            await _blobStore.DeleteBlobAsync(pathParts[0], pathParts[1], cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete blob for cancelled session {SessionId}", sessionId);
        }

        _dbContext.UploadSessions.Remove(session);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Upload session {SessionId} cancelled", sessionId);

        return NoContent();
    }

    /// <summary>
    /// List user's upload sessions
    /// </summary>
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(List<UploadSessionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UploadSessionResponse>>> ListUploadSessions(
        [FromQuery] bool includeCompleted = false,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.UploadSessions
            .Where(s => s.CreatedByOid == _currentUser.Id);

        if (!includeCompleted)
        {
            query = query.Where(s =>
                s.Status != UploadSessionStatus.Completed &&
                s.Status != UploadSessionStatus.Failed &&
                s.ExpiresAt > DateTime.UtcNow);
        }

        var sessions = await query
            .OrderByDescending(s => s.CreatedAt)
            .Take(20)
            .Select(s => new UploadSessionResponse
            {
                SessionId = s.Id,
                FileName = s.FileName,
                FileSize = s.FileSize,
                ChunkSize = s.ChunkSize,
                TotalChunks = s.TotalChunks,
                UploadedChunks = s.UploadedChunks,
                Status = s.Status.ToString(),
                ExpiresAt = s.ExpiresAt,
                BlobPath = s.BlobPath,
                VideoAssetId = s.VideoAssetId
            })
            .ToListAsync(cancellationToken);

        return Ok(sessions);
    }

    private static string GenerateBlockId(int chunkIndex)
    {
        // Block IDs must be base64 encoded and all the same length
        var blockIdString = chunkIndex.ToString("D8"); // 8 digit padding
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(blockIdString));
    }

    private static string SanitizeFileName(string fileName)
    {
        // Remove potentially dangerous characters
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());

        // Limit length
        if (sanitized.Length > 255)
        {
            var ext = Path.GetExtension(sanitized);
            sanitized = sanitized[..(255 - ext.Length)] + ext;
        }

        return sanitized;
    }
}

// Request/Response DTOs
public record CreateUploadSessionRequest
{
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long FileSize { get; init; }
    public long ChunkSize { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string[]? Tags { get; init; }
    public string? LanguageHint { get; init; }
}

public record UploadSessionResponse
{
    public Guid SessionId { get; init; }
    public required string FileName { get; init; }
    public long FileSize { get; init; }
    public long ChunkSize { get; init; }
    public int TotalChunks { get; init; }
    public int UploadedChunks { get; init; }
    public required string Status { get; init; }
    public DateTime ExpiresAt { get; init; }
    public required string BlobPath { get; init; }
    public Guid? VideoAssetId { get; init; }
}

public record ChunkUploadUrlResponse
{
    public required string Url { get; init; }
    public required string BlockId { get; init; }
    public DateTime ExpiresAt { get; init; }
    public int ChunkIndex { get; init; }
    public long ChunkSize { get; init; }
    public required Dictionary<string, string> RequiredHeaders { get; init; }
}

public record ChunkCompleteResponse
{
    public int ChunkIndex { get; init; }
    public int UploadedChunks { get; init; }
    public int TotalChunks { get; init; }
    public bool IsComplete { get; init; }
}

public record UploadCompleteResponse
{
    public Guid SessionId { get; init; }
    public Guid VideoAssetId { get; init; }
    public required string Status { get; init; }
}
