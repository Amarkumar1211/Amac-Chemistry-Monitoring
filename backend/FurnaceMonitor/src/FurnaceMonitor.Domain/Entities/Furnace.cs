using FurnaceMonitor.Domain.Enums;

namespace FurnaceMonitor.Domain.Entities;

/// <summary>
/// Master record representing a physical furnace (F1–F20+).
/// </summary>
public class Furnace : Common.EntityBase
{
    /// <summary>Short unique identifier, e.g. "F1", "F2".</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Descriptive name of the furnace.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Current operational status of the furnace.</summary>
    public FurnaceStatus Status { get; set; } = FurnaceStatus.Active;

    /// <summary>Whether the furnace master record is enabled.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Optional physical location / line.</summary>
    public string? Location { get; set; }

    /// <summary>Display order within the UI.</summary>
    public int SortOrder { get; set; }

    /// <summary>Real-time physical condition of the furnace when idle or running (Empty, Melting, Maintenance, Breakdown, Emergency, WaitingForBatch).</summary>
    public string OperationalStatus { get; set; } = "WaitingForBatch";

    /// <summary>Optional remarks or reason for status (e.g. coil maintenance, scrap delay).</summary>
    public string? OperationalRemarks { get; set; }

    /// <summary>Timestamp when operational status was last updated.</summary>
    public DateTime? OperationalStatusUpdatedAtUtc { get; set; }

    /// <summary>Navigation to batches running / owned by this furnace.</summary>
    public ICollection<FurnaceBatch> Batches { get; set; } = new List<FurnaceBatch>();
}
