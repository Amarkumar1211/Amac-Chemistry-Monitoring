using FurnaceMonitor.Domain.Enums;

namespace FurnaceMonitor.Application.DTOs.Batch;

/// <summary>
/// Represents a live batch view as shown on the operator panel / display.
/// </summary>
public class BatchDto
{
    public int Id { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public int FurnaceId { get; set; }
    public string FurnaceNumber { get; set; } = string.Empty;
    public string FurnaceName { get; set; } = string.Empty;
    public int OperatorId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public BatchStatus Status { get; set; }
    public bool IsActive { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public IndicatorColor Indicator { get; set; }
    public string? Instruction { get; set; }
    public string? Remarks { get; set; }
    public string? ClosedBy { get; set; }
    public DateTime? ClosedTimeUtc { get; set; }

    /// <summary>Selected color code combination for operator panel and display.</summary>
    public string? ColorCode { get; set; }

    /// <summary>Si reading of the current snapshot.</summary>
    public decimal Si { get; set; }

    /// <summary>Ti reading of the current snapshot.</summary>
    public decimal Ti { get; set; }

    /// <summary>Mg reading of the current snapshot.</summary>
    public decimal Mg { get; set; }

    /// <summary>Sr (Strontium) reading mapped to FutureChemical1.</summary>
    public decimal? Sr { get; set; }

    /// <summary>Future chemical fields.</summary>
    public decimal? FutureChemical1 { get; set; }
    public decimal? FutureChemical2 { get; set; }
    public decimal? FutureChemical3 { get; set; }
}

public class CreateBatchRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(50)]
    public string BatchNumber { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
    public int FurnaceId { get; set; }

    public IndicatorColor Indicator { get; set; } = IndicatorColor.Green;

    [System.ComponentModel.DataAnnotations.Range(0, 1000000, ErrorMessage = "Si value out of range.")]
    public decimal Si { get; set; }

    [System.ComponentModel.DataAnnotations.Range(0, 1000000, ErrorMessage = "Ti value out of range.")]
    public decimal Ti { get; set; }

    [System.ComponentModel.DataAnnotations.Range(0, 1000000, ErrorMessage = "Mg value out of range.")]
    public decimal Mg { get; set; }

    public decimal? Sr { get; set; }

    public decimal? FutureChemical1 { get; set; }
    public decimal? FutureChemical2 { get; set; }
    public decimal? FutureChemical3 { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(50)]
    public string? ColorCode { get; set; } = "Yellow-blue";

    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? Instruction { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(4000)]
    public string? Remarks { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? ActualOperatorName { get; set; }
}

/// <summary>
/// Payload used on the operator panel to update readings / instructions.
/// </summary>
public class UpdateBatchRequest
{
    [System.ComponentModel.DataAnnotations.MaxLength(50)]
    public string? BatchNumber { get; set; }

    [System.ComponentModel.DataAnnotations.Range(0, 1000000, ErrorMessage = "Si value out of range.")]
    public decimal Si { get; set; }

    [System.ComponentModel.DataAnnotations.Range(0, 1000000, ErrorMessage = "Ti value out of range.")]
    public decimal Ti { get; set; }

    [System.ComponentModel.DataAnnotations.Range(0, 1000000, ErrorMessage = "Mg value out of range.")]
    public decimal Mg { get; set; }

    public decimal? Sr { get; set; }

    public decimal? FutureChemical1 { get; set; }
    public decimal? FutureChemical2 { get; set; }
    public decimal? FutureChemical3 { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(50)]
    public string? ColorCode { get; set; }

    public IndicatorColor Indicator { get; set; } = IndicatorColor.Green;

    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? Instruction { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(4000)]
    public string? Remarks { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? ActualOperatorName { get; set; }
}

public class ClearFurnaceRequest
{
    public bool Confirm { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? ActualOperatorName { get; set; }
}

public class CloseBatchRequest
{
    public bool Confirm { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? ActualOperatorName { get; set; }
}
