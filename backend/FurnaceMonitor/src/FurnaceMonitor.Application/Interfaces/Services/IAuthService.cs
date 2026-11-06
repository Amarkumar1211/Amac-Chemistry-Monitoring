using FurnaceMonitor.Application.DTOs.Auth;

namespace FurnaceMonitor.Application.Interfaces.Services;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken ct = default);
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task LogoutAsync(int userId, CancellationToken ct = default);
    Task<UserDto> GetCurrentUserAsync(int userId, CancellationToken ct = default);
}
