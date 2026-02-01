using Azure.Storage.Blobs;
using T4L.VideoSearch.Api.Domain.Entities;

namespace T4L.VideoSearch.Api.Infrastructure.Services.Mock;

/// <summary>
/// Mock encoding service that simulates video transcoding for development
/// In production, this would integrate with Azure Media Services or FFmpeg
/// </summary>
public class MockEncodingService : IEncodingService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<MockEncodingService> _logger;
    private readonly IConfiguration _configuration;
    private const string HlsContainer = "hls";

    public MockEncodingService(
        BlobServiceClient blobServiceClient,
        ILogger<MockEncodingService> logger,
        IConfiguration configuration)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<EncodingResult> EncodeToVariantAsync(
        string sourceBlobPath,
        Guid videoId,
        QualityProfiles.QualityProfile profile,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Mock encoding video {VideoId} to {Quality} ({Width}x{Height} @ {Bitrate}kbps)",
            videoId, profile.Name, profile.Width, profile.Height, profile.VideoBitrateKbps);

        // Simulate encoding time based on quality (higher = longer)
        var encodingDelay = profile.Name switch
        {
            "1080p" => 3000,
            "720p" => 2000,
            "480p" => 1500,
            _ => 1000
        };

        await Task.Delay(encodingDelay, cancellationToken);

        var containerClient = _blobServiceClient.GetBlobContainerClient(HlsContainer);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        // Generate paths for HLS content
        var variantPath = $"{videoId}/{profile.Name}";
        var playlistPath = $"{variantPath}/playlist.m3u8";
        var segmentsPath = variantPath;

        // Create mock HLS playlist
        var playlistContent = GenerateMockPlaylist(videoId, profile);
        var playlistBlob = containerClient.GetBlobClient(playlistPath);
        await playlistBlob.UploadAsync(
            BinaryData.FromString(playlistContent),
            overwrite: true,
            cancellationToken: cancellationToken);

        // Create mock segments (in production, FFmpeg would generate these)
        var segmentCount = 10; // Simulated 10 segments
        var segmentDuration = 6; // 6 seconds per segment
        long totalSize = 0;

        for (int i = 0; i < segmentCount; i++)
        {
            var segmentPath = $"{segmentsPath}/segment_{i:D4}.ts";
            var segmentBlob = containerClient.GetBlobClient(segmentPath);

            // Create mock segment data (in production, this would be actual video data)
            var mockSegmentSize = profile.VideoBitrateKbps * segmentDuration * 125; // Convert kbps to bytes
            var mockData = new byte[Math.Min(mockSegmentSize, 1024)]; // Limit mock size
            Random.Shared.NextBytes(mockData);

            await segmentBlob.UploadAsync(
                BinaryData.FromBytes(mockData),
                overwrite: true,
                cancellationToken: cancellationToken);

            totalSize += mockSegmentSize;
        }

        _logger.LogInformation(
            "Mock encoding complete for video {VideoId} quality {Quality}: {SegmentCount} segments, {Size} bytes",
            videoId, profile.Name, segmentCount, totalSize);

        return new EncodingResult
        {
            Success = true,
            PlaylistPath = playlistPath,
            SegmentsPath = segmentsPath,
            FileSizeBytes = totalSize,
            SegmentCount = segmentCount
        };
    }

    public async Task<string> GenerateMasterPlaylistAsync(
        Guid videoId,
        IEnumerable<VideoVariant> variants,
        CancellationToken cancellationToken = default)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(HlsContainer);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var masterPath = $"{videoId}/master.m3u8";
        var masterContent = GenerateMasterPlaylist(variants);

        var masterBlob = containerClient.GetBlobClient(masterPath);
        await masterBlob.UploadAsync(
            BinaryData.FromString(masterContent),
            overwrite: true,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Generated master playlist for video {VideoId} with {Count} variants",
            videoId, variants.Count());

        return masterPath;
    }

    public Task<VideoMetadata> GetVideoMetadataAsync(
        string blobPath,
        CancellationToken cancellationToken = default)
    {
        // Mock metadata - in production would use FFprobe
        _logger.LogInformation("Getting mock metadata for {BlobPath}", blobPath);

        return Task.FromResult(new VideoMetadata
        {
            Width = 1920,
            Height = 1080,
            DurationMs = 180000, // 3 minutes
            Codec = "h264",
            BitrateKbps = 8000,
            FrameRate = 30.0
        });
    }

    private static string GenerateMockPlaylist(Guid videoId, QualityProfiles.QualityProfile profile)
    {
        var segmentDuration = 6;
        var segmentCount = 10;

        var playlist = new System.Text.StringBuilder();
        playlist.AppendLine("#EXTM3U");
        playlist.AppendLine("#EXT-X-VERSION:3");
        playlist.AppendLine($"#EXT-X-TARGETDURATION:{segmentDuration}");
        playlist.AppendLine("#EXT-X-MEDIA-SEQUENCE:0");
        playlist.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");

        for (int i = 0; i < segmentCount; i++)
        {
            playlist.AppendLine($"#EXTINF:{segmentDuration}.000,");
            playlist.AppendLine($"segment_{i:D4}.ts");
        }

        playlist.AppendLine("#EXT-X-ENDLIST");

        return playlist.ToString();
    }

    private static string GenerateMasterPlaylist(IEnumerable<VideoVariant> variants)
    {
        var playlist = new System.Text.StringBuilder();
        playlist.AppendLine("#EXTM3U");
        playlist.AppendLine("#EXT-X-VERSION:3");

        foreach (var variant in variants.OrderByDescending(v => v.VideoBitrateKbps))
        {
            var bandwidth = (variant.VideoBitrateKbps + variant.AudioBitrateKbps) * 1000;
            playlist.AppendLine($"#EXT-X-STREAM-INF:BANDWIDTH={bandwidth},RESOLUTION={variant.Width}x{variant.Height},NAME=\"{variant.Quality}\"");
            playlist.AppendLine($"{variant.Quality}/playlist.m3u8");
        }

        return playlist.ToString();
    }
}
