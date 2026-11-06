using FurnaceMonitor.Domain.Enums;

namespace FurnaceMonitor.Domain.Entities;

/// <summary>
/// Security role definition. System roles are seeded.
/// </summary>
public class Role : Common.EntityBase
{
    /// <summary>Unique role name (e.g. Admin, Operator, Viewer).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Human readable description of the role.</summary>
    public string? Description { get; set; }

    /// <summary>Maps to the <see cref="UserRole"/> enum value.</summary>
    public UserRole RoleType { get; set; }

    /// <summary>Navigation collection of users assigned to this role.</summary>
    public ICollection<User> Users { get; set; } = new List<User>();
}
