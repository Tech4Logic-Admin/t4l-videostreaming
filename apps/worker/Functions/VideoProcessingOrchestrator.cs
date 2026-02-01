using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace T4L.VideoSearch.Worker.Functions;

/// <summary>
/// Durable orchestration for video processing pipeline
/// </summary>
public class VideoProcessingOrchestrator
{
    private readonly ILogger<VideoProcessingOrchestrator> _logger;

    public VideoProcessingOrchestrator(ILogger<VideoProcessingOrchestrator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Main orchestration function that coordinates the video processing pipeline
    /// </summary>
    [Function(nameof(ProcessVideoOrchestration))]
    public async Task<ProcessingResult> ProcessVideoOrchestration(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var input = context.GetInput<VideoProcessingInput>()!;
        var logger = context.CreateReplaySafeLogger<VideoProcessingOrchestrator>();

        logger.LogInformation("Starting video processing for {VideoId}", input.VideoId);

        try
        {
            // Step 1: Malware Scan
            var malwareScanResult = await context.CallActivityAsync<StageResult>(
                nameof(MalwareScanActivity),
                input);

            if (!malwareScanResult.Success)
            {
                return new ProcessingResult(false, "MalwareScan", malwareScanResult.Error);
            }

            // Step 2: Content Moderation
            var moderationResult = await context.CallActivityAsync<StageResult>(
                nameof(ContentModerationActivity),
                input);

            if (!moderationResult.Success)
            {
                return new ProcessingResult(false, "ContentModeration", moderationResult.Error);
            }

            // Step 3: Transcription
            var transcriptionResult = await context.CallActivityAsync<StageResult>(
                nameof(TranscriptionActivity),
                input);

            if (!transcriptionResult.Success)
            {
                return new ProcessingResult(false, "Transcription", transcriptionResult.Error);
            }

            // Step 4: Search Indexing
            var indexingResult = await context.CallActivityAsync<StageResult>(
                nameof(SearchIndexingActivity),
                input);

            if (!indexingResult.Success)
            {
                return new ProcessingResult(false, "SearchIndexing", indexingResult.Error);
            }

            logger.LogInformation("Video processing completed successfully for {VideoId}", input.VideoId);
            return new ProcessingResult(true, "Completed", null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Video processing failed for {VideoId}", input.VideoId);
            return new ProcessingResult(false, "Unknown", ex.Message);
        }
    }

    [Function(nameof(MalwareScanActivity))]
    public Task<StageResult> MalwareScanActivity(
        [ActivityTrigger] VideoProcessingInput input,
        FunctionContext context)
    {
        var logger = context.GetLogger<VideoProcessingOrchestrator>();
        logger.LogInformation("Running malware scan for {VideoId}", input.VideoId);

        // TODO: Implement actual malware scan in Phase 3
        return Task.FromResult(new StageResult(true, null));
    }

    [Function(nameof(ContentModerationActivity))]
    public Task<StageResult> ContentModerationActivity(
        [ActivityTrigger] VideoProcessingInput input,
        FunctionContext context)
    {
        var logger = context.GetLogger<VideoProcessingOrchestrator>();
        logger.LogInformation("Running content moderation for {VideoId}", input.VideoId);

        // TODO: Implement actual content moderation in Phase 3
        return Task.FromResult(new StageResult(true, null));
    }

    [Function(nameof(TranscriptionActivity))]
    public Task<StageResult> TranscriptionActivity(
        [ActivityTrigger] VideoProcessingInput input,
        FunctionContext context)
    {
        var logger = context.GetLogger<VideoProcessingOrchestrator>();
        logger.LogInformation("Running transcription for {VideoId}", input.VideoId);

        // TODO: Implement actual transcription in Phase 3
        return Task.FromResult(new StageResult(true, null));
    }

    [Function(nameof(SearchIndexingActivity))]
    public Task<StageResult> SearchIndexingActivity(
        [ActivityTrigger] VideoProcessingInput input,
        FunctionContext context)
    {
        var logger = context.GetLogger<VideoProcessingOrchestrator>();
        logger.LogInformation("Running search indexing for {VideoId}", input.VideoId);

        // TODO: Implement actual search indexing in Phase 3
        return Task.FromResult(new StageResult(true, null));
    }

    /// <summary>
    /// HTTP trigger to start a new video processing orchestration
    /// </summary>
    [Function(nameof(StartVideoProcessing))]
    public async Task<HttpResponseData> StartVideoProcessing(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "videos/{videoId}/process")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        string videoId,
        FunctionContext context)
    {
        var logger = context.GetLogger<VideoProcessingOrchestrator>();

        var input = new VideoProcessingInput(Guid.Parse(videoId), "quarantine", $"videos/{videoId}");

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(ProcessVideoOrchestration),
            input);

        logger.LogInformation("Started orchestration with ID = {InstanceId} for video {VideoId}",
            instanceId, videoId);

        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}

public record VideoProcessingInput(Guid VideoId, string Container, string BlobPath);
public record StageResult(bool Success, string? Error);
public record ProcessingResult(bool Success, string Stage, string? Error);
