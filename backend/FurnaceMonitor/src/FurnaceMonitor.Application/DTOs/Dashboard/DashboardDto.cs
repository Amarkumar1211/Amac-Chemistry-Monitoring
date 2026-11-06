using FurnaceMonitor.Domain.Enums;

namespace FurnaceMonitor.Application.DTOs.Dashboard;

/// <summary>
/// Aggregated overview returned by the Dashboard module.
/// </summary>
public class DashboardDto
{
    public int TotalFurnaces { get; set; }
    public int ActiveFurnaces { get; set; }
    public int InactiveFurnaces { get; set; }
    public int RedCount { get; set; }
    public int YellowCount { get; set; }
    public int GreenCount { get; set; }
    public int ActiveBatches { get; set; }
    public int ConnectedDisplays { get; set; }
    public int OnlineOperators { get; set; }

    /// <summary>Furnace-by-furnace live status.</summary>
    public IReadOnlyList<FurnaceStatusCard> Furnaces { get; set; } = Array.Empty<FurnaceStatusCard>();

    /// <summary>Live activity feed of recent events.</summary>
    public IReadOnlyList<ActivityItem> RecentActivity { get; set; } = Array.Empty<ActivityItem>();
}

/// <summary>
/// A single furnace tile on the dashboard.
/// </summary>
public class FurnaceStatusCard
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public FurnaceStatus Status { get; set; }
    public string? ActiveBatchNumber { get; set; }
    public IndicatorColor? Indicator { get; set; }
}

public class ActivityItem
{
    public string UserName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
}
