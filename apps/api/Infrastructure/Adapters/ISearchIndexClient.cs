namespace T4L.VideoSearch.Api.Infrastructure.Adapters;

/// <summary>
/// Abstraction for search index operations
/// </summary>
public interface ISearchIndexClient
{
    /// <summary>
    /// Index a transcript segment
    /// </summary>
    Task IndexSegmentAsync(SearchDocument document, CancellationToken ct = default);

    /// <summary>
    /// Index multiple transcript segments in batch
    /// </summary>
    Task IndexSegmentsAsync(IEnumerable<SearchDocument> documents, CancellationToken ct = default);

    /// <summary>
    /// Search for transcript segments
    /// </summary>
    Task<SearchResults> SearchAsync(SearchQuery query, CancellationToken ct = default);

    /// <summary>
    /// Get search facets for filtering
    /// </summary>
    Task<SearchFacets> GetFacetsAsync(string userOid, string[] userGroupIds, CancellationToken ct = default);

    /// <summary>
    /// Delete all segments for a video
    /// </summary>
    Task DeleteVideoSegmentsAsync(Guid videoId, CancellationToken ct = default);

    /// <summary>
    /// Get the total count of indexed documents
    /// </summary>
    Task<long> GetDocumentCountAsync(CancellationToken ct = default);
}

public record SearchDocument(
    string Id,
    Guid VideoId,
    string VideoTitle,
    long StartMs,
    long EndMs,
    string Text,
    string? Language,
    string[] AllowedGroupIds,
    string[] AllowedUserOids,
    float[]? Vector
);

public record SearchQuery(
    string QueryText,
    string UserOid,
    string[] UserGroupIds,
    int Skip = 0,
    int Take = 20,
    string? LanguageFilter = null,
    Guid? VideoIdFilter = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    bool UseSemanticSearch = true,
    SearchSortOrder SortOrder = SearchSortOrder.Relevance
);

public enum SearchSortOrder
{
    Relevance,
    DateDesc,
    DateAsc,
    TitleAsc,
    TitleDesc
}

public record SearchResults(
    IReadOnlyList<SearchHit> Hits,
    long TotalCount,
    long LatencyMs,
    SearchFacets? Facets = null
);

public record SearchHit(
    Guid VideoId,
    string VideoTitle,
    Guid SegmentId,
    long StartMs,
    long EndMs,
    string Text,
    string? HighlightedText,
    float Score,
    string? Language = null
);

public record SearchFacets(
    IReadOnlyList<FacetValue> Languages,
    IReadOnlyList<FacetValue> Videos,
    long TotalVideos,
    long TotalSegments
);

public record FacetValue(string Value, long Count);
