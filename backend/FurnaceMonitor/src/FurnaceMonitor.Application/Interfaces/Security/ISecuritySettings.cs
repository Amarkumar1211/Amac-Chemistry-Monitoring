namespace FurnaceMonitor.Application.Interfaces.Security;

/// <summary>
/// Strongly-typed JWT configuration contract.
/// </summary>
public interface ISecuritySettings
{
    string Issuer { get; }
    string Audience { get; }
    string SecretKey { get; }
    int AccessTokenMinutes { get; }
    int RefreshTokenDays { get; }
}

/// <summary>
/// Configuration options bound from appsettings "Jwt" section.
/// </summary>
public class JwtSettings : ISecuritySettings
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 7;
}
