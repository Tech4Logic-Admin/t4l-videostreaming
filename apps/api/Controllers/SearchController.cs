using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using T4L.VideoSearch.Api.Auth;
using T4L.VideoSearch.Api.Domain.Entities;
using T4L.VideoSearch.Api.Infrastructure.Adapters;
using T4L.VideoSearch.Api.Infrastructure.Persistence;

namespace T4L.VideoSearch.Api.Controllers;

/// <summary>
/// Multilingual video search endpoints with transcript search and faceted filtering
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.CanViewVideos)]
public class SearchController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ISearchIndexClient _searchClient;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        AppDbContext dbContext,
        ISearchIndexClient searchClient,
        ICurrentUser currentUser,
        ILogger<SearchController> logger)
    {
        _dbContext = dbContext;
        _searchClient = searchClient;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Search video transcripts with timeline results
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SearchResponse>> Search(
        [FromQuery] string q,
        [FromQuery] string? language = null,
        [FromQuery] Guid? videoId = null,
        [FromQuery] string? sort = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Ok(new SearchResponse
            {
                Query = q ?? "",
                Results = [],
                TotalCount = 0,
                Page = page,
                PageSize = pageSize
            });
        }

        _logger.LogInformation("User {UserId} searching: {Query} (language: {Language}, videoId: {VideoId})",
            _currentUser.Id, q, language ?? "all", videoId?.ToString() ?? "all");

        var stopwatch = Stopwatch.StartNew();

        // Parse sort order
        var sortOrder = sort?.ToLowerInvariant() switch
        {
            "date_desc" => SearchSortOrder.DateDesc,
            "date_asc" => SearchSortOrder.DateAsc,
            "title_asc" => SearchSortOrder.TitleAsc,
            "title_desc" => SearchSortOrder.TitleDesc,
            _ => SearchSortOrder.Relevance
        };

        // Build search query with proper security context
        var searchQuery = new SearchQuery(
            QueryText: q,
            UserOid: _currentUser.Id ?? "anonymous",
            UserGroupIds: [], // Could be populated from claims
            Skip: (page - 1) * pageSize,
            Take: pageSize,
            LanguageFilter: language,
            VideoIdFilter: videoId,
            UseSemanticSearch: true,
            SortOrder: sortOrder
        );

        // Search using the search index client
        var searchResults = await _searchClient.SearchAsync(searchQuery, cancellationToken);

        stopwatch.Stop();

        // Log the search query for analytics
        await LogSearchQuery(q, language, searchResults.Hits.Count, stopwatch.ElapsedMilliseconds, cancellationToken);

        // Get video IDs from search results
        var videoIds = searchResults.Hits.Select(r => r.VideoId).Distinct().ToList();

        // Load video metadata (with security trimming)
        var videosQuery = _dbContext.VideoAssets
            .Where(v => videoIds.Contains(v.Id));

        // Security trimming: non-admins can only see published videos or their own
        if (!_currentUser.IsInAnyRole(Roles.Admin, Roles.Reviewer))
        {
            videosQuery = videosQuery.Where(v =>
                v.Status == VideoStatus.Published ||
                v.CreatedByOid == _currentUser.Id);
        }

        var videos = await videosQuery
            .ToDictionaryAsync(v => v.Id, cancellationToken);

        // Build response with timeline segments
        var results = searchResults.Hits
            .Where(r => videos.ContainsKey(r.VideoId))
            .GroupBy(r => r.VideoId)
            .Select(g => new SearchResultDto
            {
                VideoId = g.Key,
                VideoTitle = videos[g.Key].Title,
                VideoThumbnail = videos[g.Key].ThumbnailPath,
                DurationMs = videos[g.Key].DurationMs,
                LanguageHint = videos[g.Key].LanguageHint,
                Matches = g.Select(r => new TimelineMatchDto
                {
                    SegmentId = r.SegmentId,
                    StartMs = r.StartMs,
                    EndMs = r.EndMs,
                    Text = r.Text,
                    HighlightedText = r.HighlightedText,
                    Score = r.Score
                }).OrderBy(m => m.StartMs).ToList()
            })
            .ToList();

        return Ok(new SearchResponse
        {
            Query = q,
            Language = language,
            Results = results,
            TotalCount = (int)searchResults.TotalCount,
            Page = page,
            PageSize = pageSize,
            LatencyMs = searchResults.LatencyMs
        });
    }

    /// <summary>
    /// Get search suggestions (autocomplete)
    /// </summary>
    [HttpGet("suggest")]
    [ProducesResponseType(typeof(SuggestResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SuggestResponse>> Suggest(
        [FromQuery] string q,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
        {
            return Ok(new SuggestResponse { Suggestions = new List<string>() });
        }

        // Simple suggestion from recent searches and transcript text
        var suggestions = await _dbContext.TranscriptSegments
            .Where(t => t.Text.ToLower().Contains(q.ToLower()))
            .Select(t => t.Text)
            .Take(limit * 3)
            .ToListAsync(cancellationToken);

        // Extract relevant phrases containing the query
        var relevantPhrases = suggestions
            .SelectMany(s => ExtractRelevantPhrases(s, q))
            .Distinct()
            .Take(limit)
            .ToList();

        return Ok(new SuggestResponse { Suggestions = relevantPhrases });
    }

    /// <summary>
    /// Get search facets for filtering (languages, videos, counts)
    /// </summary>
    [HttpGet("facets")]
    [ProducesResponseType(typeof(FacetsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FacetsResponse>> GetFacets(CancellationToken cancellationToken)
    {
        var facets = await _searchClient.GetFacetsAsync(
            _currentUser.Id ?? "anonymous",
            [], // User group IDs
            cancellationToken);

        return Ok(new FacetsResponse
        {
            Languages = facets.Languages.Select(f => new FacetDto { Value = f.Value, Count = f.Count }).ToList(),
            Videos = facets.Videos.Select(f => new FacetDto { Value = f.Value, Count = f.Count }).ToList(),
            TotalVideos = facets.TotalVideos,
            TotalSegments = facets.TotalSegments
        });
    }

    /// <summary>
    /// Search within a specific video's transcript
    /// </summary>
    [HttpGet("video/{videoId:guid}")]
    [ProducesResponseType(typeof(VideoSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<VideoSearchResponse>> SearchInVideo(
        Guid videoId,
        [FromQuery] string q,
        CancellationToken cancellationToken)
    {
        // Get video and check access
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

        if (string.IsNullOrWhiteSpace(q))
        {
            return Ok(new VideoSearchResponse
            {
                VideoId = videoId,
                VideoTitle = video.Title,
                Query = q ?? "",
                Matches = []
            });
        }

        // Search within specific video
        var searchQuery = new SearchQuery(
            QueryText: q,
            UserOid: _currentUser.Id ?? "anonymous",
            UserGroupIds: [],
            VideoIdFilter: videoId,
            Take: 100 // Get all matches in the video
        );

        var results = await _searchClient.SearchAsync(searchQuery, cancellationToken);

        return Ok(new VideoSearchResponse
        {
            VideoId = videoId,
            VideoTitle = video.Title,
            Query = q,
            Matches = results.Hits.Select(h => new TimelineMatchDto
            {
                SegmentId = h.SegmentId,
                StartMs = h.StartMs,
                EndMs = h.EndMs,
                Text = h.Text,
                HighlightedText = h.HighlightedText,
                Score = h.Score
            }).OrderBy(m => m.StartMs).ToList()
        });
    }

    /// <summary>
    /// Get search index statistics
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(SearchStatsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SearchStatsResponse>> GetStats(CancellationToken cancellationToken)
    {
        var documentCount = await _searchClient.GetDocumentCountAsync(cancellationToken);
        var videoCount = await _dbContext.VideoAssets.CountAsync(v => v.Status == VideoStatus.Published, cancellationToken);

        // Get recent search stats
        var recentSearches = await _dbContext.SearchQueryLogs
            .Where(s => s.CreatedAt > DateTime.UtcNow.AddDays(-7))
            .GroupBy(s => 1)
            .Select(g => new
            {
                Count = g.Count(),
                AvgLatency = g.Average(s => s.LatencyMs),
                AvgResults = g.Average(s => s.ResultsCount)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new SearchStatsResponse
        {
            IndexedSegments = documentCount,
            IndexedVideos = videoCount,
            SearchesLast7Days = recentSearches?.Count ?? 0,
            AverageLatencyMs = recentSearches?.AvgLatency ?? 0,
            AverageResultCount = recentSearches?.AvgResults ?? 0
        });
    }

    private bool CanUserViewVideo(VideoAsset video)
    {
        if (_currentUser.IsInAnyRole(Roles.Admin, Roles.Reviewer))
            return true;

        if (video.CreatedByOid == _currentUser.Id)
            return true;

        return video.Status == VideoStatus.Published;
    }

    private async Task LogSearchQuery(string query, string? language, int resultsCount, long latencyMs, CancellationToken cancellationToken)
    {
        try
        {
            var searchLog = new SearchQueryLog
            {
                Id = Guid.NewGuid(),
                ActorOid = _currentUser.Id ?? "anonymous",
                Query = query,
                Language = language,
                ResultsCount = resultsCount,
                LatencyMs = latencyMs,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.SearchQueryLogs.Add(searchLog);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log search query");
        }
    }

    private static IEnumerable<string> ExtractRelevantPhrases(string text, string query)
    {
        // Simple phrase extraction around the query term
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var queryLower = query.ToLower();

        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].ToLower().Contains(queryLower))
            {
                // Get surrounding words (2 before, 2 after)
                var start = Math.Max(0, i - 2);
                var end = Math.Min(words.Length, i + 3);
                var phrase = string.Join(" ", words.Skip(start).Take(end - start));

                if (phrase.Length > 3 && phrase.Length < 100)
                {
                    yield return phrase.Trim();
                }
            }
        }
    }
}

// DTOs
public record SearchResponse
{
    public required string Query { get; init; }
    public string? Language { get; init; }
    public required List<SearchResultDto> Results { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long LatencyMs { get; init; }
}

public record SearchResultDto
{
    public Guid VideoId { get; init; }
    public required string VideoTitle { get; init; }
    public string? VideoThumbnail { get; init; }
    public long? DurationMs { get; init; }
    public string? LanguageHint { get; init; }
    public required List<TimelineMatchDto> Matches { get; init; }
}

public record TimelineMatchDto
{
    public Guid SegmentId { get; init; }
    public long StartMs { get; init; }
    public long EndMs { get; init; }
    public required string Text { get; init; }
    public string? HighlightedText { get; init; }
    public float Score { get; init; }
}

public record SuggestResponse
{
    public required List<string> Suggestions { get; init; }
}

public record FacetsResponse
{
    public required List<FacetDto> Languages { get; init; }
    public required List<FacetDto> Videos { get; init; }
    public long TotalVideos { get; init; }
    public long TotalSegments { get; init; }
}

public record FacetDto
{
    public required string Value { get; init; }
    public long Count { get; init; }
}

public record VideoSearchResponse
{
    public Guid VideoId { get; init; }
    public required string VideoTitle { get; init; }
    public required string Query { get; init; }
    public required List<TimelineMatchDto> Matches { get; init; }
}

public record SearchStatsResponse
{
    public long IndexedSegments { get; init; }
    public long IndexedVideos { get; init; }
    public int SearchesLast7Days { get; init; }
    public double AverageLatencyMs { get; init; }
    public double AverageResultCount { get; init; }
}
