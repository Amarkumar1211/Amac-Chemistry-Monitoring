using System.ComponentModel.DataAnnotations;

namespace FurnaceMonitor.Application.DTOs.Auth;

public class LoginRequest
{
    [Required(ErrorMessage = "Username is required.")]
    [MaxLength(50)]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;
}
