namespace FurnaceMonitor.Domain.Common;

/// <summary>
/// Base class providing common audit fields for all persisted entities.
/// </summary>
public abstract class EntityBase
{
    /// <summary>Primary key.</summary>
    public int Id { get; set; }

    /// <summary>UTC timestamp when the record was created.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the record was last modified.</summary>
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>User principal name that created the record.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>User principal name that last modified the record.</summary>
    public string? UpdatedBy { get; set; }
}
