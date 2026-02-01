using System.Net.Http.Headers;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using T4L.VideoSearch.Api.Auth;
using T4L.VideoSearch.Api.Domain.Entities;
using T4L.VideoSearch.Api.Infrastructure.Persistence;

namespace T4L.VideoSearch.Api.Controllers;

/// <summary>
/// Video streaming endpoints for HLS adaptive bitrate playback
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StreamController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<StreamController> _logger;
    private const string HlsContainer = "hls";

    public StreamController(
        AppDbContext dbContext,
        BlobServiceClient blobServiceClient,
        ICurrentUser currentUser,
        ILogger<StreamController> logger)
    {
        _dbContext = dbContext;
        _blobServiceClient = blobServiceClient;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Get streaming info for a video including available qualities
    /// </summary>
    [HttpGet("{videoId:guid}")]
    [AllowAnonymous] // HLS needs to be fetchable by the video element without credentials
    [ProducesResponseType(typeof(StreamingInfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<StreamingInfoResponse>> GetStreamingInfo(
        Guid videoId,
        CancellationToken cancellationToken)
    {
        var video = await _dbContext.VideoAssets
            .Include(v => v.Variants.Where(var => var.Status == VariantStatus.Completed))
            .FirstOrDefaultAsync(v => v.Id == videoId, cancellationToken);

        if (video == null)
        {
            return NotFound();
        }

        if (!CanUserViewVideo(video))
        {
            _logger.LogWarning("User {UserId} attempted to stream video {VideoId} without permission",
                _currentUser.Id, videoId);
            return Forbid();
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}/api/stream/{videoId}";

        var response = new StreamingInfoResponse
        {
            VideoId = videoId,
            Title = video.Title,
            DurationMs = video.DurationMs,
            ThumbnailUrl = $"{Request.Scheme}://{Request.Host}/api/stream/{videoId}/thumbnail",
            MasterPlaylistUrl = !string.IsNullOrEmpty(video.MasterPlaylistPath)
                ? $"{baseUrl}/master.m3u8"
                : null,
            SourceUrl = $"{baseUrl}/source",
            IsEncodingComplete = !string.IsNullOrEmpty(video.MasterPlaylistPath),
            Variants = video.Variants.Select(v => new VariantInfoDto
            {
                Quality = v.Quality,
                Width = v.Width,
                Height = v.Height,
                VideoBitrateKbps = v.VideoBitrateKbps,
                AudioBitrateKbps = v.AudioBitrateKbps,
                PlaylistUrl = $"{baseUrl}/{v.Quality}/playlist.m3u8"
            }).OrderByDescending(v => v.VideoBitrateKbps).ToList()
        };

        return Ok(response);
    }

    /// <summary>
    /// Get master HLS playlist (m3u8) for adaptive bitrate streaming
    /// </summary>
    [HttpGet("{videoId:guid}/master.m3u8")]
    [AllowAnonymous]
    [Produces("application/vnd.apple.mpegurl")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMasterPlaylist(Guid videoId, CancellationToken cancellationToken)
    {
        var video = await _dbContext.VideoAssets
            .FirstOrDefaultAsync(v => v.Id == videoId, cancellationToken);

        if (video == null || string.IsNullOrEmpty(video.MasterPlaylistPath))
        {
            return NotFound();
        }

        if (!CanUserViewVideo(video))
        {
            return Forbid();
        }

        _logger.LogDebug("Serving master playlist for video {VideoId}", videoId);

        var containerClient = _blobServiceClient.GetBlobContainerClient(HlsContainer);
        var blobClient = containerClient.GetBlobClient(video.MasterPlaylistPath);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return NotFound();
        }

        var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        return File(stream, "application/vnd.apple.mpegurl", "master.m3u8");
    }

    /// <summary>
    /// Get variant HLS playlist for a specific quality
    /// </summary>
    [HttpGet("{videoId:guid}/{quality}/playlist.m3u8")]
    [AllowAnonymous]
    [Produces("application/vnd.apple.mpegurl")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVariantPlaylist(
        Guid videoId,
        string quality,
        CancellationToken cancellationToken)
    {
        var video = await _dbContext.VideoAssets
            .FirstOrDefaultAsync(v => v.Id == videoId, cancellationToken);

        if (video == null)
        {
            return NotFound();
        }

        if (!CanUserViewVideo(video))
        {
            return Forbid();
        }

        var variant = await _dbContext.VideoVariants
            .FirstOrDefaultAsync(v => v.VideoId == videoId && v.Quality == quality, cancellationToken);

        if (variant == null || string.IsNullOrEmpty(variant.PlaylistPath))
        {
            return NotFound();
        }

        _logger.LogDebug("Serving {Quality} playlist for video {VideoId}", quality, videoId);

        var containerClient = _blobServiceClient.GetBlobContainerClient(HlsContainer);
        var blobClient = containerClient.GetBlobClient(variant.PlaylistPath);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return NotFound();
        }

        var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        return File(stream, "application/vnd.apple.mpegurl", "playlist.m3u8");
    }

    /// <summary>
    /// Get video segment (.ts file) for HLS playback
    /// </summary>
    [HttpGet("{videoId:guid}/{quality}/{segment}")]
    [AllowAnonymous]
    [Produces("video/mp2t")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSegment(
        Guid videoId,
        string quality,
        string segment,
        CancellationToken cancellationToken)
    {
        // Validate segment filename (prevent path traversal)
        if (!segment.EndsWith(".ts") || segment.Contains("..") || segment.Contains("/") || segment.Contains("\\"))
        {
            return BadRequest("Invalid segment filename");
        }

        var video = await _dbContext.VideoAssets
            .FirstOrDefaultAsync(v => v.Id == videoId, cancellationToken);

        if (video == null)
        {
            return NotFound();
        }

        if (!CanUserViewVideo(video))
        {
            return Forbid();
        }

        var segmentPath = $"{videoId}/{quality}/{segment}";

        _logger.LogDebug("Serving segment {Segment} for video {VideoId}", segment, videoId);

        var containerClient = _blobServiceClient.GetBlobContainerClient(HlsContainer);
        var blobClient = containerClient.GetBlobClient(segmentPath);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return NotFound();
        }

        var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);

        // Enable byte-range requests for seeking
        Response.Headers["Accept-Ranges"] = "bytes";

        return File(stream, "video/mp2t", segment);
    }

    /// <summary>
    /// Progressive MP4 fallback (serves original uploaded file with range support)
    /// </summary>
    [HttpGet("{videoId:guid}/source")]
    [AllowAnonymous]
    [Produces("video/mp4")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSource(
        Guid videoId,
        CancellationToken cancellationToken)
    {
        var video = await _dbContext.VideoAssets
            .FirstOrDefaultAsync(v => v.Id == videoId, cancellationToken);

        if (video == null || string.IsNullOrEmpty(video.BlobPath))
        {
            return NotFound();
        }

        // Security: allow if published or owner/reviewer/admin
        if (!CanUserViewVideo(video))
        {
            return Forbid();
        }

        var pathParts = video.BlobPath.Split('/', 2);
        var container = pathParts[0];
        var blobName = pathParts[1];

        var containerClient = _blobServiceClient.GetBlobContainerClient(container);
        var blobClient = containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            return NotFound();
        }

        var props = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        var totalLength = props.Value.ContentLength;

        // Handle Range header for seeking
        if (Request.Headers.ContainsKey("Range"))
        {
            var rangeHeader = Request.Headers.Range.ToString();
            if (RangeHeaderValue.TryParse(rangeHeader, out var range) &&
                range.Unit == "bytes" &&
                range.Ranges.Count == 1)
            {
                var rangeItem = range.Ranges.First();
                var start = rangeItem.From ?? 0;
                var end = rangeItem.To ?? (totalLength - 1);

                if (start >= totalLength)
                {
                    Response.Headers.Add("Content-Range", $"bytes */{totalLength}");
                    return StatusCode(StatusCodes.Status416RangeNotSatisfiable);
                }

                var length = end - start + 1;
                var stream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken, position: start);
                Response.Headers.Add("Accept-Ranges", "bytes");
                Response.Headers.Add("Content-Range", $"bytes {start}-{end}/{totalLength}");
                return File(stream, "video/mp4", enableRangeProcessing: true);
            }
        }

        var fullStream = await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        Response.Headers.Add("Accept-Ranges", "bytes");
        return File(fullStream, "video/mp4", enableRangeProcessing: true);
    }

    /// <summary>
    /// Get encoding status for a video
    /// </summary>
    [HttpGet("{videoId:guid}/encoding-status")]
    [Authorize(Policy = Policies.CanViewVideos)]
    [ProducesResponseType(typeof(EncodingStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EncodingStatusResponse>> GetEncodingStatus(
        Guid videoId,
        CancellationToken cancellationToken)
    {
        var video = await _dbContext.VideoAssets
            .Include(v => v.Variants)
            .FirstOrDefaultAsync(v => v.Id == videoId, cancellationToken);

        if (video == null)
        {
            return NotFound();
        }

        if (!CanUserViewVideo(video))
        {
            return Forbid();
        }

        var variants = video.Variants.ToList();
        var completed = variants.Count(v => v.Status == VariantStatus.Completed);
        var total = variants.Count;

        var response = new EncodingStatusResponse
        {
            VideoId = videoId,
            IsComplete = !string.IsNullOrEmpty(video.MasterPlaylistPath),
            ProgressPercentage = total > 0 ? (int)((double)completed / total * 100) : 0,
            Variants = variants.Select(v => new VariantStatusDto
            {
                Quality = v.Quality,
                Status = v.Status.ToString(),
                ErrorMessage = v.ErrorMessage
            }).ToList()
        };

        return Ok(response);
    }

    /// <summary>
    /// Get video thumbnail image
    /// </summary>
    [HttpGet("{videoId:guid}/thumbnail")]
    [AllowAnonymous] // Thumbnails can be public for preview
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ResponseCache(Duration = 3600)] // Cache for 1 hour
    public async Task<IActionResult> GetThumbnail(Guid videoId, CancellationToken cancellationToken)
    {
        var video = await _dbContext.VideoAssets
            .FirstOrDefaultAsync(v => v.Id == videoId, cancellationToken);

        if (video == null)
        {
            return NotFound();
        }

        _logger.LogDebug("Serving thumbnail for video {VideoId}", videoId);

        // Try to get actual thumbnail from blob storage
        if (!string.IsNullOrEmpty(video.ThumbnailPath))
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient("thumbnails");
            var blobClient = containerClient.GetBlobClient(video.ThumbnailPath);

            if (await blobClient.ExistsAsync(cancellationToken))
            {
                var download = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
                return File(download.Value.Content, "image/jpeg");
            }

            _logger.LogWarning("Thumbnail blob not found for video {VideoId}: {Path}", videoId, video.ThumbnailPath);
        }

        // Return a placeholder thumbnail (a simple gray gradient with video icon)
        return File(GeneratePlaceholderThumbnail(video.Title ?? "Video"), "image/png");
    }

    /// <summary>
    /// Generates a simple placeholder thumbnail as PNG
    /// </summary>
    private static byte[] GeneratePlaceholderThumbnail(string title)
    {
        // Create a simple 320x180 (16:9) placeholder image
        // This is a minimal PNG with a gray gradient background and "No Preview" text
        // In production, you might want to use SkiaSharp or ImageSharp for proper image generation

        // For now, return a minimal 1x1 gray PNG as a placeholder
        // This is the smallest valid PNG: 67 bytes for a 1x1 gray pixel
        // A proper implementation would generate a real thumbnail with the video title
        return new byte[] {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, // IHDR chunk
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, // 1x1 dimensions
            0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, // 8-bit RGB, no interlace
            0xDE, 0x00, 0x00, 0x00, 0x0C, 0x49, 0x44, 0x41, // IDAT chunk
            0x54, 0x08, 0xD7, 0x63, 0x68, 0x68, 0x68, 0x00, // Compressed gray pixel (0x68 = 104)
            0x00, 0x00, 0x19, 0x00, 0x07, 0x60, 0x66, 0xF9,
            0x85, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, // IEND chunk
            0x44, 0xAE, 0x42, 0x60, 0x82
        };
    }

    private bool CanUserViewVideo(VideoAsset video)
    {
        if (_currentUser.IsInAnyRole(Roles.Admin, Roles.Reviewer))
            return true;

        if (video.CreatedByOid == _currentUser.Id)
            return true;

        return video.Status == VideoStatus.Published;
    }
}

// DTOs
public record StreamingInfoResponse
{
    public Guid VideoId { get; init; }
    public required string Title { get; init; }
    public long? DurationMs { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? MasterPlaylistUrl { get; init; }
    public string? SourceUrl { get; init; }
    public bool IsEncodingComplete { get; init; }
    public required List<VariantInfoDto> Variants { get; init; }
}

public record VariantInfoDto
{
    public required string Quality { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int VideoBitrateKbps { get; init; }
    public int AudioBitrateKbps { get; init; }
    public required string PlaylistUrl { get; init; }
}

public record EncodingStatusResponse
{
    public Guid VideoId { get; init; }
    public bool IsComplete { get; init; }
    public int ProgressPercentage { get; init; }
    public required List<VariantStatusDto> Variants { get; init; }
}

public record VariantStatusDto
{
    public required string Quality { get; init; }
    public required string Status { get; init; }
    public string? ErrorMessage { get; init; }
}
