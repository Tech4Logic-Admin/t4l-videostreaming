using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Azure.Storage.Blobs;
using T4L.VideoSearch.Api.Domain.Entities;

namespace T4L.VideoSearch.Api.Infrastructure.Services;

/// <summary>
/// FFmpeg-based encoding service for real video transcoding
/// </summary>
public class FFmpegEncodingService : IEncodingService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<FFmpegEncodingService> _logger;
    private readonly string _tempPath;
    private const string HlsContainer = "hls";
    private const string VideosContainer = "videos";

    public FFmpegEncodingService(
        BlobServiceClient blobServiceClient,
        ILogger<FFmpegEncodingService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
        _tempPath = Path.Combine(Path.GetTempPath(), "t4l-encoding");
        Directory.CreateDirectory(_tempPath);
    }

    public async Task<EncodingResult> EncodeToVariantAsync(
        string sourceBlobPath,
        Guid videoId,
        QualityProfiles.QualityProfile profile,
        CancellationToken cancellationToken = default)
    {
        var workDir = Path.Combine(_tempPath, videoId.ToString(), profile.Name);
        Directory.CreateDirectory(workDir);

        try
        {
            _logger.LogInformation(
                "Starting FFmpeg encoding for video {VideoId} to {Quality} ({Width}x{Height})",
                videoId, profile.Name, profile.Width, profile.Height);

            // Download source video
            var sourceFile = Path.Combine(workDir, "source.mp4");
            await DownloadBlobAsync(sourceBlobPath, sourceFile, cancellationToken);

            // Encode to HLS
            var outputDir = Path.Combine(workDir, "output");
            Directory.CreateDirectory(outputDir);

            var playlistFile = Path.Combine(outputDir, "playlist.m3u8");
            var segmentPattern = Path.Combine(outputDir, "segment_%04d.ts");

            var ffmpegArgs = BuildFFmpegArgs(sourceFile, playlistFile, segmentPattern, profile);

            _logger.LogDebug("FFmpeg command: ffmpeg {Args}", ffmpegArgs);

            var result = await RunFFmpegAsync(ffmpegArgs, workDir, cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("FFmpeg encoding failed for video {VideoId}: {Error}", videoId, result.Error);
                return new EncodingResult
                {
                    Success = false,
                    Error = result.Error
                };
            }

            // Upload HLS files to blob storage
            var hlsContainer = _blobServiceClient.GetBlobContainerClient(HlsContainer);
            await hlsContainer.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var variantPath = $"{videoId}/{profile.Name}";
            long totalSize = 0;
            int segmentCount = 0;

            // Upload playlist
            var playlistBlob = hlsContainer.GetBlobClient($"{variantPath}/playlist.m3u8");
            await playlistBlob.UploadAsync(playlistFile, overwrite: true, cancellationToken: cancellationToken);

            // Upload segments
            foreach (var segmentFile in Directory.GetFiles(outputDir, "segment_*.ts"))
            {
                var segmentName = Path.GetFileName(segmentFile);
                var segmentBlob = hlsContainer.GetBlobClient($"{variantPath}/{segmentName}");
                await segmentBlob.UploadAsync(segmentFile, overwrite: true, cancellationToken: cancellationToken);

                var fileInfo = new FileInfo(segmentFile);
                totalSize += fileInfo.Length;
                segmentCount++;
            }

            _logger.LogInformation(
                "FFmpeg encoding complete for video {VideoId} quality {Quality}: {SegmentCount} segments, {Size} bytes",
                videoId, profile.Name, segmentCount, totalSize);

            return new EncodingResult
            {
                Success = true,
                PlaylistPath = $"{variantPath}/playlist.m3u8",
                SegmentsPath = variantPath,
                FileSizeBytes = totalSize,
                SegmentCount = segmentCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encoding video {VideoId} to {Quality}", videoId, profile.Name);
            return new EncodingResult
            {
                Success = false,
                Error = ex.Message
            };
        }
        finally
        {
            // Cleanup
            try
            {
                if (Directory.Exists(workDir))
                {
                    Directory.Delete(workDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup work directory {WorkDir}", workDir);
            }
        }
    }

    public async Task<string> GenerateMasterPlaylistAsync(
        Guid videoId,
        IEnumerable<VideoVariant> variants,
        CancellationToken cancellationToken = default)
    {
        var playlist = new StringBuilder();
        playlist.AppendLine("#EXTM3U");
        playlist.AppendLine("#EXT-X-VERSION:3");

        foreach (var variant in variants.OrderByDescending(v => v.VideoBitrateKbps))
        {
            var bandwidth = (variant.VideoBitrateKbps + variant.AudioBitrateKbps) * 1000;
            playlist.AppendLine($"#EXT-X-STREAM-INF:BANDWIDTH={bandwidth},RESOLUTION={variant.Width}x{variant.Height},NAME=\"{variant.Quality}\"");
            playlist.AppendLine($"{variant.Quality}/playlist.m3u8");
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(HlsContainer);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var masterPath = $"{videoId}/master.m3u8";
        var blob = containerClient.GetBlobClient(masterPath);
        await blob.UploadAsync(
            BinaryData.FromString(playlist.ToString()),
            overwrite: true,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Generated master playlist for video {VideoId}", videoId);

        return masterPath;
    }

    public async Task<VideoMetadata> GetVideoMetadataAsync(
        string blobPath,
        CancellationToken cancellationToken = default)
    {
        var workDir = Path.Combine(_tempPath, Guid.NewGuid().ToString());
        Directory.CreateDirectory(workDir);

        try
        {
            var sourceFile = Path.Combine(workDir, "source.mp4");
            await DownloadBlobAsync(blobPath, sourceFile, cancellationToken);

            var args = $"-v quiet -print_format json -show_format -show_streams \"{sourceFile}\"";
            var result = await RunCommandAsync("ffprobe", args, workDir, cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("FFprobe failed for {BlobPath}: {Error}", blobPath, result.Error);
                // Return empty metadata on error
                return new VideoMetadata();
            }

            // Parse ffprobe JSON output
            return ParseFFprobeOutput(result.Output);
        }
        finally
        {
            try
            {
                if (Directory.Exists(workDir))
                {
                    Directory.Delete(workDir, recursive: true);
                }
            }
            catch { }
        }
    }

    private string BuildFFmpegArgs(string input, string playlist, string segmentPattern, QualityProfiles.QualityProfile profile)
    {
        // Build FFmpeg arguments for HLS encoding
        return string.Join(" ", new[]
        {
            "-i", $"\"{input}\"",
            "-c:v", "libx264",
            "-preset", "fast",
            "-crf", "23",
            "-vf", $"scale={profile.Width}:{profile.Height}:force_original_aspect_ratio=decrease,pad={profile.Width}:{profile.Height}:(ow-iw)/2:(oh-ih)/2",
            "-b:v", $"{profile.VideoBitrateKbps}k",
            "-maxrate", $"{(int)(profile.VideoBitrateKbps * 1.5)}k",
            "-bufsize", $"{profile.VideoBitrateKbps * 2}k",
            "-c:a", "aac",
            "-b:a", $"{profile.AudioBitrateKbps}k",
            "-ar", "44100",
            "-ac", "2",
            "-f", "hls",
            "-hls_time", "6",
            "-hls_list_size", "0",
            "-hls_segment_filename", $"\"{segmentPattern}\"",
            "-hls_playlist_type", "vod",
            $"\"{playlist}\""
        });
    }

    private async Task DownloadBlobAsync(string blobPath, string localPath, CancellationToken cancellationToken)
    {
        // blobPath format: "videos/{videoId}/original.mp4" or just path
        var parts = blobPath.Split('/', 2);
        var containerName = parts.Length > 1 ? parts[0] : VideosContainer;
        var blobName = parts.Length > 1 ? parts[1] : blobPath;

        var container = _blobServiceClient.GetBlobContainerClient(containerName);
        var blob = container.GetBlobClient(blobName);

        _logger.LogDebug("Downloading blob {Container}/{Blob} to {LocalPath}", containerName, blobName, localPath);

        await using var stream = File.OpenWrite(localPath);
        await blob.DownloadToAsync(stream, cancellationToken);
    }

    private async Task<(bool Success, string Output, string? Error)> RunFFmpegAsync(
        string args, string workDir, CancellationToken cancellationToken)
    {
        return await RunCommandAsync("ffmpeg", args, workDir, cancellationToken);
    }

    private async Task<(bool Success, string Output, string? Error)> RunCommandAsync(
        string command, string args, string workDir, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("{Command} exited with code {ExitCode}: {Error}", command, process.ExitCode, error);
            return (false, output, error);
        }

        return (true, output, null);
    }

    private VideoMetadata ParseFFprobeOutput(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            long durationMs = 0;
            int width = 0, height = 0;
            string? codec = null;

            // Get duration from format
            if (root.TryGetProperty("format", out var format) &&
                format.TryGetProperty("duration", out var durationProp))
            {
                if (double.TryParse(durationProp.GetString(), out var durationSec))
                {
                    durationMs = (long)(durationSec * 1000);
                }
            }

            // Get video stream info
            if (root.TryGetProperty("streams", out var streams))
            {
                foreach (var stream in streams.EnumerateArray())
                {
                    if (stream.TryGetProperty("codec_type", out var codecType) &&
                        codecType.GetString() == "video")
                    {
                        if (stream.TryGetProperty("width", out var w))
                            width = w.GetInt32();
                        if (stream.TryGetProperty("height", out var h))
                            height = h.GetInt32();
                        if (stream.TryGetProperty("codec_name", out var c))
                            codec = c.GetString();
                        break;
                    }
                }
            }

            return new VideoMetadata
            {
                DurationMs = durationMs,
                Width = width,
                Height = height,
                Codec = codec
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse FFprobe output");
            return new VideoMetadata();
        }
    }
}
