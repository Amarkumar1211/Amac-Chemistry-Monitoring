using FurnaceMonitor.Domain.Enums;

namespace FurnaceMonitor.Domain.Entities;

/// <summary>
/// Chemical and instruction snapshot for a batch.
/// The current reading and historical snapshots both live here.
/// </summary>
public class FurnaceChemical : Common.EntityBase
{
    /// <summary>Upper bound used to sanity-check numeric chemical values. Increased to 100000 to support grams.</summary>
    public const decimal MaxChemicalValue = 100000m;

    /// <summary>Foreign key to the parent batch.</summary>
    public int BatchId { get; set; }

    /// <summary>Navigation to the parent batch.</summary>
    public FurnaceBatch? Batch { get; set; }

    /// <summary>Silicon reading.</summary>
    public decimal Si { get; set; }

    /// <summary>Titanium reading.</summary>
    public decimal Ti { get; set; }

    /// <summary>Magnesium reading.</summary>
    public decimal Mg { get; set; }

    /// <summary>Placeholder for future chemical fields.</summary>
    public decimal? FutureChemical1 { get; set; }

    /// <summary>Placeholder for future chemical fields.</summary>
    public decimal? FutureChemical2 { get; set; }

    /// <summary>Placeholder for future chemical fields.</summary>
    public decimal? FutureChemical3 { get; set; }

    /// <summary>Indicator colour associated with this snapshot.</summary>
    public IndicatorColor Indicator { get; set; } = IndicatorColor.Green;

    /// <summary>Instruction text at the time of this snapshot.</summary>
    public string? Instruction { get; set; }

    /// <summary>Remarks at the time of this snapshot.</summary>
    public string? Remarks { get; set; }

    /// <summary>Color code selected for this snapshot.</summary>
    public string? ColorCode { get; set; }

    /// <summary>UTC time of the snapshot. Used to calculate history duration.</summary>
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;
}
