namespace T4L.VideoSearch.Api.Domain.Entities;

/// <summary>
/// Daily aggregated metrics for reporting
/// </summary>
public class DailyMetrics
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public DateOnly Date { get; set; }
    public int Uploads { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int Quarantined { get; set; }
    public double AvgIndexTimeMs { get; set; }
    public int Searches { get; set; }
    public int Errors { get; set; }
    public long TotalVideosDurationMs { get; set; }
    public int UniqueUsers { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
