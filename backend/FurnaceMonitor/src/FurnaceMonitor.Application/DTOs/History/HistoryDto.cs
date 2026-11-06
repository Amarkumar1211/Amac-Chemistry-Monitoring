using FurnaceMonitor.Domain.Enums;

namespace FurnaceMonitor.Application.DTOs.History;

/// <summary>
/// A completed batch record used in the History module.
/// </summary>
public class BatchHistoryDto
{
    public int Id { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public string FurnaceNumber { get; set; } = string.Empty;
    public string FurnaceName { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public BatchStatus Status { get; set; }
    public string? ClosedBy { get; set; }
    public DateTime? ClosedTimeUtc { get; set; }

/// <summary>Elapsed duration of the batch, when it has ended.</summary>
    public TimeSpan? Duration => EndedAtUtc.HasValue ? EndedAtUtc.Value - StartedAtUtc : null;

    /// <summary>Elapsed duration in whole minutes (0 when not ended).</summary>
    public int DurationMinutes { get; set; }

    /// <summary>Final indicator colour.</summary>
    public IndicatorColor Indicator { get; set; }

    /// <summary>Latest chemical readings for the batch.</summary>
    public decimal Si { get; set; }
    public decimal Ti { get; set; }
    public decimal Mg { get; set; }
    public decimal Sr { get; set; }

    public string? Instruction { get; set; }
    public string? Remarks { get; set; }

    /// <summary>Snapshot history for this batch.</summary>
    public IReadOnlyList<ChemicalHistoryDto> ChemicalHistory { get; set; } = Array.Empty<ChemicalHistoryDto>();
}

/// <summary>
/// A single chemical / instruction snapshot in history.
/// </summary>
public class ChemicalHistoryDto
{
    public int Id { get; set; }
    public decimal Si { get; set; }
    public decimal Ti { get; set; }
    public decimal Mg { get; set; }
    public decimal? FutureChemical1 { get; set; }
    public decimal? FutureChemical2 { get; set; }
    public decimal? FutureChemical3 { get; set; }
    public IndicatorColor Indicator { get; set; }
    public string? Instruction { get; set; }
    public string? Remarks { get; set; }
    public DateTime CapturedAtUtc { get; set; }
}

public class HistoryQuery
{
    public int? FurnaceId { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// A single audit trail entry.
/// </summary>
public class AuditLogDto
{
    public int Id { get; set; }
    public string? UserName { get; set; }
    public AuditAction Action { get; set; }
    public string? Description { get; set; }
    public string? Entity { get; set; }
    public string? EntityId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

/// <summary>
/// Aggregated chemical consumption for a specific period.
/// </summary>
public class ChemicalConsumptionPeriodDto
{
    public string Period { get; set; } = string.Empty;
    public Dictionary<string, decimal> Consumptions { get; set; } = new();
}
