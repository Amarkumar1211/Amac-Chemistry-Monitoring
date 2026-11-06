namespace FurnaceMonitor.Domain.Enums;

/// <summary>
/// Lifecycle status of a furnace master record.
/// </summary>
public enum FurnaceStatus
{
    /// <summary>Furnace is operational.</summary>
    Active = 1,

    /// <summary>Furnace is under maintenance.</summary>
    Maintenance = 2,

    /// <summary>Furnace has been decommissioned.</summary>
    Inactive = 3
}
