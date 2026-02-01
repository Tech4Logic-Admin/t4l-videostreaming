namespace T4L.VideoSearch.Api.Domain.Entities;

/// <summary>
/// Log of search queries for analytics
/// </summary>
public class SearchQueryLog
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string ActorOid { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string? Language { get; set; }
    public int ResultsCount { get; set; }
    public long LatencyMs { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
