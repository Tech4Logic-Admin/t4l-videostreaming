using Microsoft.EntityFrameworkCore;
using T4L.VideoSearch.Api.Domain.Entities;
using T4L.VideoSearch.Api.Infrastructure.BackgroundJobs.Jobs;
using T4L.VideoSearch.Api.Infrastructure.Persistence;
using T4L.VideoSearch.Api.Infrastructure.Services;

namespace T4L.VideoSearch.Api.Infrastructure.BackgroundJobs.Handlers;

/// <summary>
/// Handles extraction of AI-powered highlights and summaries from video transcripts
/// </summary>
public class ExtractHighlightsJobHandler : IJobHandler<ExtractHighlightsJob>
{
    private readonly AppDbContext _dbContext;
    private readonly IAIHighlightsService _highlightsService;
    private readonly ILogger<ExtractHighlightsJobHandler> _logger;

    public ExtractHighlightsJobHandler(
        AppDbContext dbContext,
        IAIHighlightsService highlightsService,
        ILogger<ExtractHighlightsJobHandler> logger)
    {
        _dbContext = dbContext;
        _highlightsService = highlightsService;
        _logger = logger;
    }

    public async Task HandleAsync(ExtractHighlightsJob job, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting AI highlights extraction for video {VideoId}", job.VideoAssetId);

        var processingJob = await _dbContext.VideoProcessingJobs
            .FirstOrDefaultAsync(j => j.Id == job.ProcessingJobId, cancellationToken);

        if (processingJob == null)
        {
            _logger.LogWarning("Processing job {ProcessingJobId} not found", job.ProcessingJobId);
            return;
        }

        processingJob.Status = JobStatus.InProgress;
        processingJob.StartedAt = DateTime.UtcNow;
        processingJob.Attempts++;
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            // Get the video with transcript segments
            var video = await _dbContext.VideoAssets
                .Include(v => v.TranscriptSegments)
                .FirstOrDefaultAsync(v => v.Id == job.VideoAssetId, cancellationToken);

            if (video == null)
            {
                throw new InvalidOperationException($"Video {job.VideoAssetId} not found");
            }

            if (!video.TranscriptSegments.Any())
            {
                _logger.LogWarning("No transcript segments found for video {VideoId}", job.VideoAssetId);
                processingJob.Status = JobStatus.Completed;
                processingJob.CompletedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            // Combine transcript segments into full text
            var orderedSegments = video.TranscriptSegments.OrderBy(s => s.StartMs).ToList();
            var fullTranscript = string.Join("\n", orderedSegments.Select(s =>
                $"[{FormatTimestamp(s.StartMs)}] {s.Text}"));

            // Get detected language from segments
            var detectedLanguage = orderedSegments
                .Where(s => !string.IsNullOrEmpty(s.DetectedLanguage))
                .GroupBy(s => s.DetectedLanguage)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key ?? video.LanguageHint;

            _logger.LogInformation(
                "Extracting highlights for video {VideoId}, transcript length: {Length}, language: {Language}",
                job.VideoAssetId, fullTranscript.Length, detectedLanguage);

            // Extract highlights using AI (always in English with original text preserved)
            var highlightsResult = await _highlightsService.ExtractHighlightsAsync(
                fullTranscript, detectedLanguage, cancellationToken);

            if (highlightsResult.Success)
            {
                // Remove existing highlights and translations
                var existingHighlights = await _dbContext.VideoHighlights
                    .Include(h => h.Translations)
                    .Where(h => h.VideoId == job.VideoAssetId)
                    .ToListAsync(cancellationToken);

                _dbContext.VideoHighlights.RemoveRange(existingHighlights);

                // Add new highlights (Text is always in English for search)
                foreach (var highlight in highlightsResult.Highlights)
                {
                    var videoHighlight = new VideoHighlight
                    {
                        Id = Guid.NewGuid(),
                        VideoId = job.VideoAssetId,
                        Text = highlight.Text, // English text for search indexing
                        OriginalText = highlight.OriginalText, // Original language text
                        SourceLanguage = highlightsResult.SourceLanguage,
                        Category = highlight.Category,
                        Importance = highlight.Importance,
                        TimestampMs = highlight.TimestampMs,
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.VideoHighlights.Add(videoHighlight);
                }

                _logger.LogInformation(
                    "Extracted {Count} highlights for video {VideoId} (English with source: {SourceLang})",
                    highlightsResult.Highlights.Count, job.VideoAssetId, highlightsResult.SourceLanguage);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to extract highlights for video {VideoId}: {Error}",
                    job.VideoAssetId, highlightsResult.Error);
            }

            // Generate summary (always in English with original text preserved)
            var summaryResult = await _highlightsService.GenerateSummaryAsync(
                fullTranscript, video.Title, cancellationToken);

            if (summaryResult.Success)
            {
                // Remove existing summary
                var existingSummary = await _dbContext.VideoSummaries
                    .FirstOrDefaultAsync(s => s.VideoId == job.VideoAssetId, cancellationToken);

                if (existingSummary != null)
                {
                    _dbContext.VideoSummaries.Remove(existingSummary);
                }

                // Add new summary (English for search, with original language preserved)
                var videoSummary = new VideoSummary
                {
                    Id = Guid.NewGuid(),
                    VideoId = job.VideoAssetId,
                    Summary = summaryResult.Summary, // English for search
                    TlDr = summaryResult.TlDr, // English for search
                    OriginalSummary = summaryResult.OriginalSummary, // Original language
                    OriginalTlDr = summaryResult.OriginalTlDr, // Original language
                    SourceLanguage = summaryResult.SourceLanguage,
                    Keywords = summaryResult.Keywords.ToArray(),
                    Topics = highlightsResult.Topics?.ToArray() ?? [],
                    Sentiment = highlightsResult.Sentiment,
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.VideoSummaries.Add(videoSummary);

                // Auto-generate tags from keywords if video has no user-defined tags
                if (video.Tags.Length == 0 && summaryResult.Keywords.Count > 0)
                {
                    video.Tags = summaryResult.Keywords.Take(10).ToArray();
                    video.UpdatedAt = DateTime.UtcNow;
                    _logger.LogInformation(
                        "Auto-generated {Count} tags for video {VideoId}: {Tags}",
                        video.Tags.Length, job.VideoAssetId, string.Join(", ", video.Tags));
                }

                // Auto-generate description from TL;DR if video has no description
                if (string.IsNullOrEmpty(video.Description) && !string.IsNullOrEmpty(summaryResult.TlDr))
                {
                    video.Description = summaryResult.TlDr;
                    video.UpdatedAt = DateTime.UtcNow;
                    _logger.LogInformation(
                        "Auto-generated description for video {VideoId} from TL;DR",
                        job.VideoAssetId);
                }

                _logger.LogInformation(
                    "Generated summary for video {VideoId}: {TlDr}",
                    job.VideoAssetId, summaryResult.TlDr);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to generate summary for video {VideoId}: {Error}",
                    job.VideoAssetId, summaryResult.Error);
            }

            processingJob.Status = JobStatus.Completed;
            processingJob.CompletedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("AI highlights extraction completed for video {VideoId}", job.VideoAssetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract highlights for video {VideoId}", job.VideoAssetId);

            processingJob.Status = JobStatus.Failed;
            processingJob.LastError = ex.Message;
            processingJob.CompletedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            throw;
        }
    }

    private static string FormatTimestamp(long ms)
    {
        var ts = TimeSpan.FromMilliseconds(ms);
        return ts.Hours > 0
            ? $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes}:{ts.Seconds:D2}";
    }
}
