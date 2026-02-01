namespace T4L.VideoSearch.Api.Infrastructure.Adapters.Local;

/// <summary>
/// Mock video indexer for local development - generates fake transcript segments
/// </summary>
public class MockVideoIndexerClient : IVideoIndexerClient
{
    private readonly ILogger<MockVideoIndexerClient> _logger;
    private readonly Dictionary<string, MockJob> _jobs = new();

    public MockVideoIndexerClient(ILogger<MockVideoIndexerClient> logger)
    {
        _logger = logger;
    }

    public Task<string> SubmitVideoAsync(VideoIndexRequest request, CancellationToken ct = default)
    {
        var jobId = Guid.NewGuid().ToString();

        _jobs[jobId] = new MockJob
        {
            VideoId = request.VideoId,
            SubmittedAt = DateTime.UtcNow,
            State = IndexingState.Processing
        };

        _logger.LogInformation("Mock Video Indexer: Submitted video {VideoId} as job {JobId}",
            request.VideoId, jobId);

        return Task.FromResult(jobId);
    }

    public Task<VideoIndexStatus> GetStatusAsync(string jobId, CancellationToken ct = default)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
        {
            return Task.FromResult(new VideoIndexStatus(jobId, IndexingState.Failed, 0, "Job not found"));
        }

        // Simulate processing time (complete after 3 seconds)
        var elapsed = DateTime.UtcNow - job.SubmittedAt;
        if (elapsed.TotalSeconds > 3)
        {
            job.State = IndexingState.Completed;
        }

        var progress = job.State == IndexingState.Completed ? 100
            : Math.Min(99, (int)(elapsed.TotalSeconds / 3 * 100));

        return Task.FromResult(new VideoIndexStatus(jobId, job.State, progress, null));
    }

    public Task<VideoIndexResult?> GetResultsAsync(string jobId, CancellationToken ct = default)
    {
        if (!_jobs.TryGetValue(jobId, out var job) || job.State != IndexingState.Completed)
        {
            return Task.FromResult<VideoIndexResult?>(null);
        }

        // Generate mock transcript
        var transcript = GenerateMockTranscript();

        var result = new VideoIndexResult(
            jobId,
            DurationMs: 120000, // 2 minutes
            DetectedLanguage: "en",
            Transcript: transcript,
            Keywords: ["technology", "video", "search", "demo"]
        );

        _logger.LogInformation("Mock Video Indexer: Returning results for job {JobId} with {SegmentCount} segments",
            jobId, transcript.Count);

        return Task.FromResult<VideoIndexResult?>(result);
    }

    private static List<TranscriptItem> GenerateMockTranscript()
    {
        var sentences = new[]
        {
            "Welcome to the Tech4Logic Video Search demonstration.",
            "This platform allows you to search through video content with ease.",
            "Our advanced AI transcribes videos in multiple languages.",
            "You can jump to specific moments in any video using timeline search.",
            "The system supports role-based access control for enterprise security.",
            "Content moderation ensures all videos meet policy requirements.",
            "Search results show relevant segments with highlighted text.",
            "Click any result to jump directly to that moment in the video.",
            "The admin dashboard provides comprehensive analytics and reporting.",
            "Thank you for watching this demonstration of our capabilities."
        };

        var transcript = new List<TranscriptItem>();
        long currentMs = 0;

        foreach (var sentence in sentences)
        {
            var durationMs = 8000 + Random.Shared.Next(4000); // 8-12 seconds per segment
            transcript.Add(new TranscriptItem(
                StartMs: currentMs,
                EndMs: currentMs + durationMs,
                Text: sentence,
                Speaker: "Speaker 1",
                Confidence: 0.85f + (float)Random.Shared.NextDouble() * 0.15f
            ));
            currentMs += durationMs + 500; // 500ms gap
        }

        return transcript;
    }

    private class MockJob
    {
        public Guid VideoId { get; set; }
        public DateTime SubmittedAt { get; set; }
        public IndexingState State { get; set; }
    }
}
