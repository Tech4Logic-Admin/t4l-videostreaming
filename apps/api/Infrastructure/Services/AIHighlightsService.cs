using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace T4L.VideoSearch.Api.Infrastructure.Services;

/// <summary>
/// Service for extracting AI-powered highlights and key points from video transcripts
/// </summary>
public interface IAIHighlightsService
{
    /// <summary>
    /// Extract key points and highlights from a transcript
    /// </summary>
    Task<HighlightsResult> ExtractHighlightsAsync(
        string transcript,
        string? languageHint = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a summary of the video content
    /// </summary>
    Task<SummaryResult> GenerateSummaryAsync(
        string transcript,
        string? title = null,
        CancellationToken cancellationToken = default);
}

public class HighlightsResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public List<Highlight> Highlights { get; init; } = [];
    public List<string> Topics { get; init; } = [];
    public string? Sentiment { get; init; }
    public string? SourceLanguage { get; init; }
}

public class Highlight
{
    /// <summary>
    /// Highlight text in English (for search indexing)
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Original text in source language (if different from English)
    /// </summary>
    public string? OriginalText { get; init; }

    public string Category { get; init; } = string.Empty; // "key_point", "promise", "announcement", "statistic", "quote"
    public long? TimestampMs { get; init; }
    public float? Importance { get; init; } // 0-1 score
}

public class SummaryResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }

    /// <summary>
    /// Summary in English (for search indexing)
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// TL;DR in English (for search indexing)
    /// </summary>
    public string? TlDr { get; init; }

    /// <summary>
    /// Original summary in source language
    /// </summary>
    public string? OriginalSummary { get; init; }

    /// <summary>
    /// Original TL;DR in source language
    /// </summary>
    public string? OriginalTlDr { get; init; }

    /// <summary>
    /// Source language code
    /// </summary>
    public string? SourceLanguage { get; init; }

    public List<string> Keywords { get; init; } = [];
}

/// <summary>
/// Implementation using OpenAI GPT for AI highlights extraction
/// </summary>
public class OpenAIHighlightsService : IAIHighlightsService
{
    private readonly ILogger<OpenAIHighlightsService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public OpenAIHighlightsService(
        ILogger<OpenAIHighlightsService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient("OpenAI");
    }

    public async Task<HighlightsResult> ExtractHighlightsAsync(
        string transcript,
        string? languageHint = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("OpenAI API key not configured");
            return new HighlightsResult
            {
                Success = false,
                Error = "OpenAI API key not configured"
            };
        }

        try
        {
            var systemPrompt = @"You are an expert at analyzing video transcripts and extracting key information.
Extract the following from the transcript:
1. Key points - main ideas or important statements
2. Promises - any commitments or promises made
3. Announcements - new information or declarations
4. Statistics - any numbers, data, or metrics mentioned
5. Notable quotes - memorable or impactful statements

Also identify:
- Main topics discussed
- Overall sentiment (positive, negative, neutral, mixed)

IMPORTANT: Always provide ALL highlights in ENGLISH, regardless of the transcript's language.
If the transcript is not in English, translate the highlights to English.
Also include the original text in the source language.

Respond in JSON format:
{
  ""highlights"": [
    {""text"": ""English text..."", ""original_text"": ""Original language text (if not English)..."", ""category"": ""key_point|promise|announcement|statistic|quote"", ""importance"": 0.0-1.0}
  ],
  ""topics"": [""topic1 in English"", ""topic2 in English""],
  ""sentiment"": ""positive|negative|neutral|mixed"",
  ""source_language"": ""detected language code (en, hi, gu, ta, te, bn, mr, etc.)""
}

Be concise but comprehensive. Focus on the most important points.";

            var userPrompt = $"Analyze this transcript and extract highlights (provide English translations for non-English content):\n\n{transcript}";

            if (!string.IsNullOrEmpty(languageHint) && languageHint != "en")
            {
                userPrompt += $"\n\nNote: The transcript appears to be in {languageHint}. Please extract highlights in English with original text preserved.";
            }

            var requestBody = new
            {
                model = _configuration["OpenAI:Model"] ?? "gpt-4o-mini",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.3,
                max_tokens = 2000,
                response_format = new { type = "json_object" }
            };

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _httpClient.PostAsync(
                "https://api.openai.com/v1/chat/completions",
                new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("OpenAI API error: {Status} - {Error}", response.StatusCode, errorContent);
                return new HighlightsResult
                {
                    Success = false,
                    Error = $"OpenAI API error: {response.StatusCode}"
                };
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var chatResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(jsonContent);

            var content = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrEmpty(content))
            {
                return new HighlightsResult { Success = false, Error = "Empty response from OpenAI" };
            }

            var highlightsJson = JsonSerializer.Deserialize<HighlightsJsonResponse>(content);

            return new HighlightsResult
            {
                Success = true,
                Highlights = highlightsJson?.Highlights?.Select(h => new Highlight
                {
                    Text = h.Text,
                    OriginalText = h.OriginalText,
                    Category = h.Category,
                    Importance = h.Importance
                }).ToList() ?? [],
                Topics = highlightsJson?.Topics ?? [],
                Sentiment = highlightsJson?.Sentiment,
                SourceLanguage = highlightsJson?.SourceLanguage ?? languageHint ?? "en"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract highlights");
            return new HighlightsResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<SummaryResult> GenerateSummaryAsync(
        string transcript,
        string? title = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
        {
            return new SummaryResult
            {
                Success = false,
                Error = "OpenAI API key not configured"
            };
        }

        try
        {
            var systemPrompt = @"You are an expert at summarizing video content.
Generate:
1. A comprehensive summary (2-3 paragraphs) in ENGLISH
2. A TL;DR (1-2 sentences) in ENGLISH
3. Key keywords/tags (5-10 words) in ENGLISH

IMPORTANT: Always provide the summary and TL;DR in ENGLISH, regardless of the transcript's language.
If the transcript is not in English, also include the original language versions.

Respond in JSON format:
{
  ""summary"": ""English summary..."",
  ""tldr"": ""English TL;DR..."",
  ""original_summary"": ""Original language summary (if not English)..."",
  ""original_tldr"": ""Original language TL;DR (if not English)..."",
  ""source_language"": ""detected language code (en, hi, gu, ta, te, bn, mr, etc.)"",
  ""keywords"": [""keyword1"", ""keyword2""]
}";

            var userPrompt = string.IsNullOrEmpty(title)
                ? $"Summarize this video transcript (provide English translations for non-English content):\n\n{transcript}"
                : $"Summarize this video titled \"{title}\" (provide English translations for non-English content):\n\n{transcript}";

            var requestBody = new
            {
                model = _configuration["OpenAI:Model"] ?? "gpt-4o-mini",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = 0.5,
                max_tokens = 1000,
                response_format = new { type = "json_object" }
            };

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _httpClient.PostAsync(
                "https://api.openai.com/v1/chat/completions",
                new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("OpenAI API error: {Status} - {Error}", response.StatusCode, errorContent);
                return new SummaryResult { Success = false, Error = $"OpenAI API error: {response.StatusCode}" };
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var chatResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(jsonContent);

            var content = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrEmpty(content))
            {
                return new SummaryResult { Success = false, Error = "Empty response" };
            }

            var summaryJson = JsonSerializer.Deserialize<SummaryJsonResponse>(content);

            return new SummaryResult
            {
                Success = true,
                Summary = summaryJson?.Summary,
                TlDr = summaryJson?.TlDr,
                OriginalSummary = summaryJson?.OriginalSummary,
                OriginalTlDr = summaryJson?.OriginalTlDr,
                SourceLanguage = summaryJson?.SourceLanguage ?? "en",
                Keywords = summaryJson?.Keywords ?? []
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate summary");
            return new SummaryResult { Success = false, Error = ex.Message };
        }
    }
}

// JSON response models
internal class ChatCompletionResponse
{
    [JsonPropertyName("choices")]
    public List<ChatChoice>? Choices { get; set; }
}

internal class ChatChoice
{
    [JsonPropertyName("message")]
    public ChatMessage? Message { get; set; }
}

internal class ChatMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}

internal class HighlightsJsonResponse
{
    [JsonPropertyName("highlights")]
    public List<HighlightJson>? Highlights { get; set; }

    [JsonPropertyName("topics")]
    public List<string>? Topics { get; set; }

    [JsonPropertyName("sentiment")]
    public string? Sentiment { get; set; }

    [JsonPropertyName("source_language")]
    public string? SourceLanguage { get; set; }
}

internal class HighlightJson
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("original_text")]
    public string? OriginalText { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("importance")]
    public float? Importance { get; set; }
}

internal class SummaryJsonResponse
{
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("tldr")]
    public string? TlDr { get; set; }

    [JsonPropertyName("original_summary")]
    public string? OriginalSummary { get; set; }

    [JsonPropertyName("original_tldr")]
    public string? OriginalTlDr { get; set; }

    [JsonPropertyName("source_language")]
    public string? SourceLanguage { get; set; }

    [JsonPropertyName("keywords")]
    public List<string>? Keywords { get; set; }
}
