namespace T4L.VideoSearch.Api.Infrastructure.Services.Mock;

/// <summary>
/// Mock transcription service for development and testing.
/// Generates realistic-looking transcript data without actual transcription.
/// </summary>
public class MockTranscriptionService : ITranscriptionService
{
    private readonly ILogger<MockTranscriptionService> _logger;
    private readonly Random _random = new();

    // Sample phrases for generating mock transcripts
    private static readonly string[] SamplePhrases =
    [
        "Welcome to this video presentation.",
        "Let me show you how this works.",
        "As you can see on the screen,",
        "This is an important concept to understand.",
        "Moving on to the next topic,",
        "Here's a key takeaway from this section.",
        "Let me explain this in more detail.",
        "Now let's look at some examples.",
        "This feature is particularly useful for",
        "In conclusion, we've covered",
        "Thanks for watching this video.",
        "If you have any questions,",
        "Feel free to reach out to our team.",
        "The process is straightforward.",
        "Let's start with the basics.",
        "Here are the main points to remember.",
        "This demonstrates the core functionality.",
        "As shown in the diagram,",
        "The next step involves",
        "We recommend following these guidelines."
    ];

    public MockTranscriptionService(ILogger<MockTranscriptionService> logger)
    {
        _logger = logger;
    }

    public async Task<TranscriptResult> TranscribeAsync(
        string blobPath,
        string? languageHint = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Mock transcription started for blob: {BlobPath}, language hint: {LanguageHint}",
            blobPath,
            languageHint ?? "auto");

        // Simulate processing time (1-3 seconds)
        await Task.Delay(_random.Next(1000, 3000), cancellationToken);

        // Generate mock duration (1-5 minutes in milliseconds)
        var durationMs = _random.Next(60_000, 300_000);

        // Generate mock segments
        var segments = GenerateMockSegments(durationMs);

        _logger.LogInformation(
            "Mock transcription completed: {SegmentCount} segments, duration {DurationMs}ms",
            segments.Count,
            durationMs);

        return new TranscriptResult
        {
            Success = true,
            DetectedLanguage = languageHint ?? "en",
            DurationMs = durationMs,
            Segments = segments
        };
    }

    private List<TranscriptSegmentData> GenerateMockSegments(long durationMs)
    {
        var segments = new List<TranscriptSegmentData>();
        var currentMs = 0L;
        var speakers = new[] { "Speaker 1", "Speaker 2", null };

        while (currentMs < durationMs)
        {
            // Segment duration: 2-8 seconds
            var segmentDuration = _random.Next(2000, 8000);
            var endMs = Math.Min(currentMs + segmentDuration, durationMs);

            // Pick a random phrase (or combine 2-3 for longer segments)
            var phraseCount = _random.Next(1, 4);
            var text = string.Join(" ", Enumerable.Range(0, phraseCount)
                .Select(_ => SamplePhrases[_random.Next(SamplePhrases.Length)]));

            segments.Add(new TranscriptSegmentData
            {
                StartMs = currentMs,
                EndMs = endMs,
                Text = text,
                Speaker = speakers[_random.Next(speakers.Length)],
                Confidence = 0.85f + (float)(_random.NextDouble() * 0.15) // 0.85-1.0
            });

            // Add a small gap between segments (0-500ms)
            currentMs = endMs + _random.Next(0, 500);
        }

        return segments;
    }
}
