using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using T4L.VideoSearch.Api.Auth;
using T4L.VideoSearch.Api.Domain.Entities;
using T4L.VideoSearch.Api.Infrastructure.Persistence;

namespace T4L.VideoSearch.Api.Controllers;

/// <summary>
/// Audit log endpoints for administrators
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.RequireAdmin)]
public class AuditController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<AuditController> _logger;

    public AuditController(AppDbContext dbContext, ILogger<AuditController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Get audit logs with filtering
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(AuditLogResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuditLogResponse>> GetAuditLogs(
        [FromQuery] string? action = null,
        [FromQuery] string? targetType = null,
        [FromQuery] Guid? targetId = null,
        [FromQuery] string? actorOid = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var query = _dbContext.AuditLogs.AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(action))
        {
            query = query.Where(l => l.Action == action);
        }

        if (!string.IsNullOrEmpty(targetType))
        {
            query = query.Where(l => l.TargetType == targetType);
        }

        if (targetId.HasValue)
        {
            query = query.Where(l => l.TargetId == targetId.Value);
        }

        if (!string.IsNullOrEmpty(actorOid))
        {
            query = query.Where(l => l.ActorOid == actorOid);
        }

        if (from.HasValue)
        {
            query = query.Where(l => l.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(l => l.CreatedAt <= to.Value);
        }

        // Get total count for pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Get page of results
        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new AuditLogDto
            {
                Id = l.Id,
                ActorOid = l.ActorOid,
                Action = l.Action,
                TargetType = l.TargetType,
                TargetId = l.TargetId,
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Admin retrieved {Count} audit logs (page {Page})", logs.Count, page);

        return Ok(new AuditLogResponse
        {
            Items = logs,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        });
    }

    /// <summary>
    /// Get audit log details
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AuditLogDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditLogDetailDto>> GetAuditLogDetail(Guid id, CancellationToken cancellationToken)
    {
        var log = await _dbContext.AuditLogs
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        if (log == null)
        {
            return NotFound();
        }

        return Ok(new AuditLogDetailDto
        {
            Id = log.Id,
            ActorOid = log.ActorOid,
            Action = log.Action,
            TargetType = log.TargetType,
            TargetId = log.TargetId,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent,
            Metadata = log.Metadata?.RootElement.ToString(),
            CreatedAt = log.CreatedAt
        });
    }

    /// <summary>
    /// Get audit log summary statistics
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(AuditStatsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuditStatsResponse>> GetAuditStats(
        [FromQuery] int days = 7,
        CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, 90);
        var since = DateTime.UtcNow.AddDays(-days);

        var stats = await _dbContext.AuditLogs
            .Where(l => l.CreatedAt >= since)
            .GroupBy(l => l.Action)
            .Select(g => new ActionCount { Action = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var totalActions = stats.Sum(s => s.Count);
        var uniqueActors = await _dbContext.AuditLogs
            .Where(l => l.CreatedAt >= since)
            .Select(l => l.ActorOid)
            .Distinct()
            .CountAsync(cancellationToken);

        // Security events (failed logins, rate limits, etc.)
        var securityEventCount = await _dbContext.AuditLogs
            .Where(l => l.CreatedAt >= since)
            .Where(l => l.Action.Contains("failed") || l.Action.Contains("blocked") || l.Action.Contains("rate_limit"))
            .CountAsync(cancellationToken);

        return Ok(new AuditStatsResponse
        {
            PeriodDays = days,
            TotalActions = totalActions,
            UniqueActors = uniqueActors,
            SecurityEvents = securityEventCount,
            ActionBreakdown = stats,
            TopActions = stats.OrderByDescending(s => s.Count).Take(10).ToList()
        });
    }

    /// <summary>
    /// Get recent activity for a specific target
    /// </summary>
    [HttpGet("target/{targetType}/{targetId:guid}")]
    [ProducesResponseType(typeof(List<AuditLogDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<AuditLogDto>>> GetTargetActivity(
        string targetType,
        Guid targetId,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);

        var logs = await _dbContext.AuditLogs
            .Where(l => l.TargetType == targetType && l.TargetId == targetId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(limit)
            .Select(l => new AuditLogDto
            {
                Id = l.Id,
                ActorOid = l.ActorOid,
                Action = l.Action,
                TargetType = l.TargetType,
                TargetId = l.TargetId,
                IpAddress = l.IpAddress,
                UserAgent = l.UserAgent,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(logs);
    }

    /// <summary>
    /// Export audit logs as CSV
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportAuditLogs(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? action = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AuditLogs.AsQueryable();

        if (from.HasValue)
        {
            query = query.Where(l => l.CreatedAt >= from.Value);
        }
        else
        {
            // Default to last 30 days
            query = query.Where(l => l.CreatedAt >= DateTime.UtcNow.AddDays(-30));
        }

        if (to.HasValue)
        {
            query = query.Where(l => l.CreatedAt <= to.Value);
        }

        if (!string.IsNullOrEmpty(action))
        {
            query = query.Where(l => l.Action == action);
        }

        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Take(10000) // Limit export size
            .ToListAsync(cancellationToken);

        // Generate CSV
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Id,Timestamp,Actor,Action,TargetType,TargetId,IpAddress");

        foreach (var log in logs)
        {
            csv.AppendLine($"{log.Id},{log.CreatedAt:O},{log.ActorOid},{log.Action},{log.TargetType},{log.TargetId},{log.IpAddress}");
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", $"audit_logs_{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}

// DTOs
public class AuditLogResponse
{
    public List<AuditLogDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class AuditLogDto
{
    public Guid Id { get; set; }
    public string ActorOid { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public Guid? TargetId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AuditLogDetailDto : AuditLogDto
{
    public string? Metadata { get; set; }
}

public class AuditStatsResponse
{
    public int PeriodDays { get; set; }
    public int TotalActions { get; set; }
    public int UniqueActors { get; set; }
    public int SecurityEvents { get; set; }
    public List<ActionCount> ActionBreakdown { get; set; } = [];
    public List<ActionCount> TopActions { get; set; } = [];
}

public class ActionCount
{
    public string Action { get; set; } = string.Empty;
    public int Count { get; set; }
}
