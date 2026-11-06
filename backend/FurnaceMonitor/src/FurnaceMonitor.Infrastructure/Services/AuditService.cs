using FurnaceMonitor.Application.Interfaces.Repositories;
using FurnaceMonitor.Application.Interfaces.Services;
using FurnaceMonitor.Domain.Entities;
using FurnaceMonitor.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FurnaceMonitor.Infrastructure.Persistence;

namespace FurnaceMonitor.Infrastructure.Services;

/// <summary>
/// Writes append-only audit log entries for security and business events.
/// </summary>
public class AuditService : IAuditService
{
    private readonly IUnitOfWork _uow;
    private readonly UnitOfWorkAccessor _accessor;
    private readonly ILogger<AuditService> _logger;

    public AuditService(IUnitOfWork uow, UnitOfWorkAccessor accessor, ILogger<AuditService> logger)
    {
        _uow = uow;
        _accessor = accessor;
        _logger = logger;
    }

    public async Task LogAsync(AuditAction action, string? actor, string? description, string? entityType = null, int? entityId = null, CancellationToken ct = default)
    {
        try
        {
            var entry = new AuditLog
            {
                UserName = actor,
                Action = action,
                Description = description,
                Entity = entityType,
                EntityId = entityId?.ToString(),
                OccurredAtUtc = DateTime.UtcNow
            };
            await _uow.AuditLogs.AddAsync(entry, ct);
            await _uow.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Audit logging must never break a primary operation.
            _logger.LogError(ex, "Failed to write audit log for action {Action} by {Actor}.", action, actor);
        }
    }

    public async Task<IReadOnlyList<object>> RecentAsync(int count = 50, CancellationToken ct = default)
    {
        var rows = await _accessor.DbContext.AuditLogs
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(count)
            .Select(a => new
            {
                a.Id,
                a.UserName,
                a.Action,
                a.Description,
                a.Entity,
                a.OccurredAtUtc
            })
            .ToListAsync(ct);

return rows.Cast<object>().ToList();
    }
}
