namespace FurnaceMonitor.Domain.Entities;

/// <summary>
/// Tracks live SignalR connections for observability and diagnostics.
/// </summary>
public class SignalRConnection : Common.EntityBase
{
    /// <summary>Connection id assigned by the SignalR hub.</summary>
    public string ConnectionId { get; set; } = string.Empty;

/// <summary>User id associated with the connection (nullable for anonymous).</summary>
    public int? UserId { get; set; }

    /// <summary>Display name of the user associated with the connection.</summary>
    public string? UserName { get; set; }

    /// <summary>Furnace group the client subscribed to, e.g. "F1".</summary>
    public string? FurnaceNumber { get; set; }

    /// <summary>UTC time the connection was established.</summary>
    public DateTime ConnectedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>UTC time the connection was terminated, if any.</summary>
    public DateTime? DisconnectedAtUtc { get; set; }

    /// <summary>Whether the connection is currently live.</summary>
    public bool IsConnected { get; set; } = true;

    /// <summary>User agent string of the client.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Client IP address.</summary>
    public string? IpAddress { get; set; }
}
