using FurnaceMonitor.Domain.Enums;

namespace FurnaceMonitor.Application.Interfaces.Services;

public interface IAuditService
{
    Task LogAsync(AuditAction action, string? actor, string? description, string? entityType = null, int? entityId = null, CancellationToken ct = default);
    Task<IReadOnlyList<object>> RecentAsync(int count = 50, CancellationToken ct = default);
}
