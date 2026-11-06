namespace FurnaceMonitor.Domain.Entities;

/// <summary>
/// Tracks an authenticated user session for observability and security.
/// </summary>
public class UserSession : Common.EntityBase
{
    /// <summary>Foreign key to the <see cref="User"/>.</summary>
    public int UserId { get; set; }

    /// <summary>Navigation to the user.</summary>
    public User? User { get; set; }

    /// <summary>JWT id (jti) associated with the session.</summary>
    public string? JwtId { get; set; }

    /// <summary>Client IP address.</summary>
    public string? IpAddress { get; set; }

    /// <summary>User agent string.</summary>
    public string? UserAgent { get; set; }

    /// <summary>UTC time the session started.</summary>
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>UTC time the session ended, if any.</summary>
    public DateTime? EndedAtUtc { get; set; }

    /// <summary>Whether the session is still active.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Refresh token issued to support token rotation.</summary>
    public string? RefreshToken { get; set; }

    /// <summary>UTC expiry of the current refresh token.</summary>
    public DateTime? RefreshTokenExpiryUtc { get; set; }
}
