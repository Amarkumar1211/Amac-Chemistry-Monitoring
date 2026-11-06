using FurnaceMonitor.Domain.Enums;

namespace FurnaceMonitor.Domain.Entities;

/// <summary>
/// Append-only audit trail recording security and business actions.
/// </summary>
public class AuditLog : Common.EntityBase
{
    /// <summary>User principal responsible for the action (nullable for system).</summary>
    public string? UserName { get; set; }

    /// <summary>Foreign key to the user, when known.</summary>
    public int? UserId { get; set; }

    /// <summary>Classification of the action.</summary>
    public AuditAction Action { get; set; }

    /// <summary>Human readable description of what happened.</summary>
    public string? Description { get; set; }

    /// <summary>Affected entity type, e.g. "FurnaceBatch".</summary>
    public string? Entity { get; set; }

    /// <summary>Identifier of the affected entity.</summary>
    public string? EntityId { get; set; }

    /// <summary>Client IP address.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Serialized before/after payload for auditability.</summary>
    public string? Payload { get; set; }

    /// <summary>UTC timestamp of the action.</summary>
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
