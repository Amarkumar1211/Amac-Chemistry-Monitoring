using FurnaceMonitor.Domain.Entities;

namespace FurnaceMonitor.Application.Interfaces.Security;

/// <summary>
/// Abstraction over JWT access + refresh token generation.
/// </summary>
public interface ITokenService
{
    string GenerateAccessToken(User user, string roleName, string jwtId);
    string GenerateRefreshToken();
    int? ValidateRefreshTokenGetUserId(string refreshToken);
}
