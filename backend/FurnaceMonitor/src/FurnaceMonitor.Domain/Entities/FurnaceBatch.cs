using System.ComponentModel.DataAnnotations.Schema;
using FurnaceMonitor.Domain.Enums;

namespace FurnaceMonitor.Domain.Entities;

/// <summary>
/// A batch run inside a furnace. Only one batch may be active per furnace.
/// </summary>
public class FurnaceBatch : Common.EntityBase
{
    /// <summary>Unique, human readable batch number.</summary>
    public string BatchNumber { get; set; } = string.Empty;

    /// <summary>Foreign key to <see cref="Furnace"/>.</summary>
    public int FurnaceId { get; set; }

    /// <summary>Navigation to the furnace.</summary>
    public Furnace? Furnace { get; set; }

    /// <summary>Foreign key to the owning <see cref="User"/>.</summary>
    public int OperatorId { get; set; }

    /// <summary>Navigation to the user running this batch.</summary>
    public User? Operator { get; set; }

    /// <summary>Current lifecycle status of the batch.</summary>
    public BatchStatus Status { get; set; } = BatchStatus.Running;

    /// <summary>Whether the batch is currently the active batch for its furnace.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when the batch started.</summary>
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the batch ended (closed or cleared). Null while running.</summary>
    public DateTime? EndedAtUtc { get; set; }

    /// <summary>Current indicator colour for the batch.</summary>
    public IndicatorColor Indicator { get; set; } = IndicatorColor.Green;

    /// <summary>Current operator instruction text.</summary>
    public string? Instruction { get; set; }

    /// <summary>Current operator remarks.</summary>
    public string? Remarks { get; set; }

    /// <summary>User who closed the batch.</summary>
    public string? ClosedBy { get; set; }

    /// <summary>UTC timestamp of closure, if any.</summary>
    public DateTime? ClosedTimeUtc { get; set; }

    /// <summary>Current chemical reading for this batch.</summary>
    [NotMapped]
    public FurnaceChemical? Chemical { get; set; }

    /// <summary>Selected color code combination for operator panel and display.</summary>
    public string? ColorCode { get; set; } = "Yellow-blue";

    /// <summary>Historical snapshots of this batch over time.</summary>
    public ICollection<FurnaceChemical>? ChemicalHistory { get; set; } = new List<FurnaceChemical>();
}
