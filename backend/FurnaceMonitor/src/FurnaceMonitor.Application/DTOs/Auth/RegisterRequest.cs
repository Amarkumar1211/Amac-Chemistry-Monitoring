using System.ComponentModel.DataAnnotations;

namespace FurnaceMonitor.Application.DTOs.Auth;

public class RegisterRequest
{
    [Required(ErrorMessage = "Username is required.")]
    [MaxLength(50)]
    [RegularExpression(@"^[a-zA-Z0-9_.]+$", ErrorMessage = "Username may only contain letters, numbers, dots and underscores.")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    [MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Display name is required.")]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "A valid email is required.")]
    [MaxLength(150)]
    public string? Email { get; set; }

    /// <summary>Role id. Defaults to Viewer if not supplied.</summary>
    public int? RoleId { get; set; }
}
