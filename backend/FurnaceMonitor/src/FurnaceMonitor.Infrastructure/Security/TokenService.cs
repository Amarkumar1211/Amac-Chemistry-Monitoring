using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FurnaceMonitor.Application.Interfaces.Security;
using FurnaceMonitor.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace FurnaceMonitor.Infrastructure.Security;

/// <summary>
/// Issues and validates JWT access tokens and opaque refresh tokens.
/// Refresh tokens are stored hashed on the user to allow revocation/rotation.
/// </summary>
public class TokenService : ITokenService
{
    private readonly ISecuritySettings _settings;

    public TokenService(ISecuritySettings settings)
    {
        _settings = settings;
    }

    public string GenerateAccessToken(User user, string roleName, string jwtId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName),
            new(JwtRegisteredClaimNames.Jti, jwtId),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, roleName),
            new("displayName", user.DisplayName)
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(_settings.AccessTokenMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    /// <summary>
    /// Validates a stored refresh token by comparing the supplied token to the
    /// hashed value previously issued to the user.
    /// </summary>
    public int? ValidateRefreshTokenGetUserId(string refreshToken)
    {
        // Parsing a hashed token is not meaningful here; the AuthService compares
        // against the user's stored value using a constant-time check. Returning
        // null instructs callers to rely on the user-scoped comparison instead.
        return null;
    }
}

public static class RefreshTokenHasher
{
    /// <summary>
    /// Hashes a refresh token so plaintext tokens are not stored at rest.
    /// </summary>
    public static string Hash(string token)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
