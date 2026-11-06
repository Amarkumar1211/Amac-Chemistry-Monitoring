namespace FurnaceMonitor.Domain.Enums;

/// <summary>
/// Classification of an auditable action for the audit trail.
/// </summary>
public enum AuditAction
{
    /// <summary>User authenticated successfully.</summary>
    Login = 1,

    /// <summary>User logged out.</summary>
    Logout = 2,

    /// <summary>A new record was created.</summary>
    Create = 3,

    /// <summary>An existing record was modified.</summary>
    Update = 4,

    /// <summary>A record was cleared / deactivated.</summary>
    Clear = 5,

    /// <summary>A batch was closed.</summary>
    Close = 6,

    /// <summary>A SignalR connection was established.</summary>
    SignalRConnected = 7,

    /// <summary>A SignalR connection was terminated.</summary>
    SignalRDisconnected = 8,

    /// <summary>A token was refreshed.</summary>
    TokenRefresh = 9,

    /// <summary>An unexpected error was handled.</summary>
    Error = 10,

    /// <summary>A record was permanently deleted.</summary>
    Delete = 11
}
