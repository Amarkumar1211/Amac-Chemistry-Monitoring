using FurnaceMonitor.Domain.Enums;

namespace FurnaceMonitor.Application.DTOs.Furnace;

public class FurnaceDto
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public FurnaceStatus Status { get; set; }
    public bool IsActive { get; set; }
    public string? Location { get; set; }
    public int SortOrder { get; set; }

    /// <summary>Id of the currently active batch, if any.</summary>
    public int? ActiveBatchId { get; set; }

    /// <summary>Number of the currently active batch, if any.</summary>
    public string? ActiveBatchNumber { get; set; }

    /// <summary>Real-time physical condition of the furnace (Empty, Melting, Maintenance, Breakdown, Emergency, WaitingForBatch).</summary>
    public string OperationalStatus { get; set; } = "WaitingForBatch";

    /// <summary>Optional remarks or reason for status.</summary>
    public string? OperationalRemarks { get; set; }

    /// <summary>Timestamp when operational status was last updated.</summary>
    public DateTime? OperationalStatusUpdatedAtUtc { get; set; }
}

public class UpdateFurnaceOperationalStatusRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(50)]
    public string OperationalStatus { get; set; } = "WaitingForBatch";

    [System.ComponentModel.DataAnnotations.MaxLength(250)]
    public string? OperationalRemarks { get; set; }
}

public class SaveFurnaceRequest
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(10)]
    public string Number { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public FurnaceStatus Status { get; set; } = FurnaceStatus.Active;

    public bool IsActive { get; set; } = true;

    [System.ComponentModel.DataAnnotations.MaxLength(150)]
    public string? Location { get; set; }

    public int SortOrder { get; set; }
}

