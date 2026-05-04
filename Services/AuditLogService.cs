using System.Text.Json;
using LIS.Data;
using LIS.Models;
using Microsoft.EntityFrameworkCore;

namespace LIS.Services;

public class AuditLogService
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;

    public AuditLogService(IDbContextFactory<ApplicationDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task WriteAsync(string action, string entityType, string entityId, ApplicationUser? actor, object? before = null, object? after = null, object? metadata = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        context.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            PerformedByUserId = actor?.Id ?? "system",
            PerformedByName = actor?.FullName ?? actor?.Email ?? "System",
            PerformedAt = DateTime.UtcNow,
            BeforeJson = before == null ? null : JsonSerializer.Serialize(before),
            AfterJson = after == null ? null : JsonSerializer.Serialize(after),
            MetadataJson = metadata == null ? null : JsonSerializer.Serialize(metadata)
        });

        await context.SaveChangesAsync();
    }

    public async Task<List<AuditLog>> GetAsync(AuditLogFilter filter)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Action))
            query = query.Where(log => log.Action.Contains(filter.Action));

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
            query = query.Where(log => log.EntityType.Contains(filter.EntityType));

        if (!string.IsNullOrWhiteSpace(filter.PerformedByUserId))
            query = query.Where(log => log.PerformedByUserId == filter.PerformedByUserId);

        if (filter.From.HasValue)
            query = query.Where(log => log.PerformedAt >= filter.From.Value);

        if (filter.To.HasValue)
            query = query.Where(log => log.PerformedAt <= filter.To.Value);

        return await query
            .OrderByDescending(log => log.PerformedAt)
            .Take(filter.PageSize)
            .ToListAsync();
    }
}

public class AuditLogFilter
{
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public string? PerformedByUserId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int PageSize { get; set; } = 200;
}
