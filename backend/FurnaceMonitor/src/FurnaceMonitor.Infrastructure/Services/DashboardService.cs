using FurnaceMonitor.Application.DTOs.Dashboard;
using FurnaceMonitor.Application.Interfaces.Services;
using FurnaceMonitor.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FurnaceMonitor.Infrastructure.Persistence;

namespace FurnaceMonitor.Infrastructure.Services;

/// <summary>
/// Aggregates live operational data for the dashboard and its real-time updates.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly UnitOfWorkAccessor _accessor;
    private readonly ISignalRConnectionService _signalR;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(UnitOfWorkAccessor accessor, ISignalRConnectionService signalR, ILogger<DashboardService> logger)
    {
        _accessor = accessor;
        _signalR = signalR;
        _logger = logger;
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var db = _accessor.DbContext;

        var totalFurnaces = await db.Furnaces.CountAsync(ct);
        var activeFurnaces = await db.Furnaces.CountAsync(f => f.IsActive, ct);
        var inactiveFurnaces = totalFurnaces - activeFurnaces;

        var activeBatches = await db.FurnaceBatches
            .Where(b => b.IsActive && b.Status == BatchStatus.Running)
            .ToListAsync(ct);

        var redCount = activeBatches.Count(b => b.Indicator == IndicatorColor.Red);
        var yellowCount = activeBatches.Count(b => b.Indicator == IndicatorColor.Yellow);
        var greenCount = activeBatches.Count(b => b.Indicator == IndicatorColor.Green);
        var currentBatches = activeBatches.Count;

        var connectedDisplays = await _signalR.CountActiveConnectionsAsync(ct);
        var onlineOperators = await db.UserSessions.CountAsync(s => s.IsActive, ct);

        // Furnace tiles with active batch status
        var furnaceCards = await db.Furnaces
            .AsNoTracking()
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Number)
            .Select(f => new FurnaceStatusCard
            {
                Id = f.Id,
                Number = f.Number,
                Name = f.Name,
                Status = f.Status,
                ActiveBatchNumber = f.Batches.Where(b => b.IsActive && b.Status == BatchStatus.Running).Select(b => b.BatchNumber).FirstOrDefault(),
                Indicator = f.Batches.Where(b => b.IsActive && b.Status == BatchStatus.Running).Select(b => b.Indicator).Cast<IndicatorColor?>().FirstOrDefault()
            })
            .ToListAsync(ct);

        // Recent activity feed
        var recent = await db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(20)
            .Select(a => new ActivityItem
            {
                UserName = a.UserName ?? "system",
                Description = a.Description ?? string.Empty,
                OccurredAtUtc = a.OccurredAtUtc
            })
            .ToListAsync(ct);

        var dto = new DashboardDto
        {
            TotalFurnaces = totalFurnaces,
            ActiveFurnaces = activeFurnaces,
            InactiveFurnaces = inactiveFurnaces,
            RedCount = redCount,
            YellowCount = yellowCount,
            GreenCount = greenCount,
            ActiveBatches = currentBatches,
            ConnectedDisplays = connectedDisplays,
            OnlineOperators = onlineOperators,
            Furnaces = furnaceCards,
            RecentActivity = recent
        };

        _logger.LogDebug("Dashboard generated: {Batches} active batches, {Furnaces} furnaces.", currentBatches, totalFurnaces);
        return dto;
    }
}
