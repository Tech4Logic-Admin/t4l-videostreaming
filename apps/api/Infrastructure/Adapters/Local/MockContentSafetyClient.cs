namespace T4L.VideoSearch.Api.Infrastructure.Adapters.Local;

/// <summary>
/// Mock content safety client for local development
/// Simulates Azure Content Safety API behavior with trigger words for testing
/// </summary>
public class MockContentSafetyClient : IContentSafetyClient
{
    private readonly ILogger<MockContentSafetyClient> _logger;

    // Trigger words for testing moderation (case-insensitive)
    private static readonly Dictionary<string, (string Category, ContentSafetySeverity Severity)> TriggerWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // High severity triggers
        ["unsafe_test_content"] = ("Violence", ContentSafetySeverity.High),
        ["harmful_content_test"] = ("SelfHarm", ContentSafetySeverity.High),
        ["hate_speech_test"] = ("Hate", ContentSafetySeverity.High),

        // Medium severity triggers
        ["moderate_flag_test"] = ("Violence", ContentSafetySeverity.Medium),
        ["adult_content_test"] = ("Sexual", ContentSafetySeverity.Medium),

        // Low severity triggers
        ["mild_flag_test"] = ("Violence", ContentSafetySeverity.Low),
        ["profanity_test"] = ("Profanity", ContentSafetySeverity.Low)
    };

    public MockContentSafetyClient(ILogger<MockContentSafetyClient> logger)
    {
        _logger = logger;
    }

    public Task<ContentSafetyResult> AnalyzeTextAsync(string text, CancellationToken ct = default)
    {
        _logger.LogDebug("Mock Content Safety: Analyzing text ({Length} chars)", text.Length);

        var flaggedCategories = new List<ContentSafetyCategory>();
        var overallSeverity = ContentSafetySeverity.None;

        // Check for trigger words
        foreach (var (trigger, (category, severity)) in TriggerWords)
        {
            if (text.Contains(trigger, StringComparison.OrdinalIgnoreCase))
            {
                flaggedCategories.Add(new ContentSafetyCategory(
                    category,
                    severity,
                    severity switch
                    {
                        ContentSafetySeverity.High => 0.9f,
                        ContentSafetySeverity.Medium => 0.6f,
                        ContentSafetySeverity.Low => 0.3f,
                        _ => 0f
                    }
                ));

                if (severity > overallSeverity)
                {
                    overallSeverity = severity;
                }

                _logger.LogInformation("Mock Content Safety: Flagged '{Trigger}' as {Category} ({Severity})",
                    trigger, category, severity);
            }
        }

        var isSafe = flaggedCategories.Count == 0;

        return Task.FromResult(new ContentSafetyResult(
            IsSafe: isSafe,
            OverallSeverity: overallSeverity,
            Categories: flaggedCategories
        ));
    }

    public Task<ContentSafetyResult> AnalyzeImageAsync(byte[] imageData, CancellationToken ct = default)
    {
        _logger.LogDebug("Mock Content Safety: Analyzing image ({Size} bytes)", imageData.Length);

        // Mock: always return safe for images
        return Task.FromResult(new ContentSafetyResult(
            IsSafe: true,
            OverallSeverity: ContentSafetySeverity.None,
            Categories: []
        ));
    }

    public Task<ContentSafetyResult> AnalyzeImageUrlAsync(string imageUrl, CancellationToken ct = default)
    {
        _logger.LogDebug("Mock Content Safety: Analyzing image from URL {Url}", imageUrl);

        // Mock: always return safe for image URLs
        return Task.FromResult(new ContentSafetyResult(
            IsSafe: true,
            OverallSeverity: ContentSafetySeverity.None,
            Categories: []
        ));
    }
}
