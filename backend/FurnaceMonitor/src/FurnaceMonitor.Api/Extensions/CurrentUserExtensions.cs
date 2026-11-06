using System.Security.Claims;

namespace FurnaceMonitor.Api.Extensions;

/// <summary>
/// Helpers for extracting the authenticated user identity from the current claims.
/// </summary>
public static class CurrentUserExtensions
{
    /// <summary>Returns the user id from the NameIdentifier claim.</summary>
    public static int? UserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : null;
    }

    /// <summary>Returns the user's display name (falls back to the user name).</summary>
    public static string UserDisplayName(this ClaimsPrincipal principal)
    {
        var display = principal.FindFirstValue("displayName");
        if (!string.IsNullOrWhiteSpace(display)) return display;
        return principal.Identity?.Name ?? "system";
    }
}

