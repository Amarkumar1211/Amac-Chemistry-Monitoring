namespace FurnaceMonitor.Domain.Enums;

/// <summary>
/// Defines the built-in security roles used across the application.
/// </summary>
public enum UserRole
{
    /// <summary>Full management rights: manages users, furnaces and history.</summary>
    Admin = 1,

    /// <summary>Can add, update, clear furnaces and view assigned furnaces.</summary>
    Operator = 2,

    /// <summary>Read-only access for wall/display screens.</summary>
    Viewer = 3
}
