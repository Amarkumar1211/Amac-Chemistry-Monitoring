using FurnaceMonitor.Domain.Enums;

namespace FurnaceMonitor.Application.DTOs.Realtime;

/// <summary>
/// Realtime payload pushed via SignalR to subscribed furnace groups.
/// </summary>
public class FurnaceUpdateDto
{
    public string FurnaceNumber { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public decimal Si { get; set; }
    public decimal Ti { get; set; }
    public decimal Mg { get; set; }
    public decimal? Sr { get; set; }
    public decimal? FutureChemical1 { get; set; }
    public decimal? FutureChemical2 { get; set; }
    public decimal? FutureChemical3 { get; set; }
    public string? ColorCode { get; set; }
    public IndicatorColor Indicator { get; set; }
    public string IndicatorName => Indicator.ToString();
    public string? Instruction { get; set; }
    public string? Remarks { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Realtime payload notifying subscribers that a furnace has been cleared.
/// </summary>
public class FurnaceClearedDto
{
    public string FurnaceNumber { get; set; } = string.Empty;
    public DateTime ClearedAtUtc { get; set; } = DateTime.UtcNow;
    public string Message { get; set; } = "No Active Batch";
    public string OperationalStatus { get; set; } = "Empty";
}

/// <summary>
/// Realtime payload notifying subscribers that a furnace operational status has changed.
/// </summary>
public class FurnaceStatusChangedDto
{
    public int FurnaceId { get; set; }
    public string FurnaceNumber { get; set; } = string.Empty;
    public string OperationalStatus { get; set; } = "WaitingForBatch";
    public string? OperationalRemarks { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
