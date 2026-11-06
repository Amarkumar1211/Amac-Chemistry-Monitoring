namespace FurnaceMonitor.Domain.Entities;

/// <summary>
/// Application user account (Admin, Operator or Viewer).
/// </summary>
public class User : Common.EntityBase
{
    /// <summary>
    /// Max length allowed for the BCrypt password hash stored against a user.
    /// </summary>
    public const int MaxPasswordHashLength = 200;

    /// <summary>Login username, guaranteed unique.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>BCrypt hash of the user's password.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional contact email.</summary>
    public string? Email { get; set; }

    /// <summary>Whether the account is enabled for login.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Whether the account is a built-in system account that cannot be deleted.</summary>
    public bool IsSystem { get; set; }

    /// <summary>UTC timestamp of the last successful login.</summary>
    public DateTime? LastLoginAtUtc { get; set; }

    /// <summary>Foreign key to <see cref="Role"/>.</summary>
    public int RoleId { get; set; }

    /// <summary>Navigation to the assigned role.</summary>
    public Role? Role { get; set; }

    /// <summary>Navigation to batches created/owned by this user.</summary>
    public ICollection<FurnaceBatch> Batches { get; set; } = new List<FurnaceBatch>();

    /// <summary>Navigation to active sessions for this user.</summary>
    public ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
}
