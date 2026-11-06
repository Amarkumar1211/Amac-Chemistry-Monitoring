using FurnaceMonitor.Application.DTOs.Auth;
using FurnaceMonitor.Application.Exceptions;
using FurnaceMonitor.Application.Interfaces.Repositories;
using FurnaceMonitor.Application.Interfaces.Security;
using FurnaceMonitor.Application.Interfaces.Services;
using FurnaceMonitor.Domain.Entities;
using FurnaceMonitor.Domain.Enums;
using FurnaceMonitor.Infrastructure.Security;
using Microsoft.Extensions.Logging;

namespace FurnaceMonitor.Infrastructure.Services;

/// <summary>
/// Handles authentication: login, register, token refresh, logout.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IAuditService _audit;
    private readonly ISecuritySettings _securitySettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUnitOfWork uow,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IAuditService audit,
        ISecuritySettings securitySettings,
        ILogger<AuthService> logger)
    {
        _uow = uow;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _audit = audit;
        _securitySettings = securitySettings;
        _logger = logger;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        var user = await _uow.Users.FirstOrDefaultAsync(u => u.UserName == request.UserName, ct, "Role");
        if (user is null || !user.IsActive)
            throw new UnauthorizedException("Invalid username or password.");

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for user {UserName} from {Ip}", request.UserName, ipAddress);
            throw new UnauthorizedException("Invalid username or password.");
        }

        var roleName = user.Role?.Name ?? "Viewer";
        var jwtId = Guid.NewGuid().ToString("N");
        var accessToken = _tokenService.GenerateAccessToken(user, roleName, jwtId);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.LastLoginAtUtc = DateTime.UtcNow;
        _uow.Users.Update(user);

        // Limit concurrent sessions for Admin and Operator
        if (user.Role?.RoleType == UserRole.Admin || user.Role?.RoleType == UserRole.Operator)
        {
            var activeSessions = await _uow.UserSessions.ListAsync(s => s.UserId == user.Id && s.IsActive, ct);
            if (activeSessions.Count >= 2)
            {
                var sessionsToClose = activeSessions.OrderBy(s => s.StartedAtUtc).Take(activeSessions.Count - 1);
                foreach (var s in sessionsToClose)
                {
                    s.IsActive = false;
                    s.EndedAtUtc = DateTime.UtcNow;
                    _uow.UserSessions.Update(s);
                }
            }
        }

        // Track session
        await _uow.UserSessions.AddAsync(new UserSession
        {
            UserId = user.Id,
            JwtId = jwtId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            StartedAtUtc = DateTime.UtcNow,
            IsActive = true,
            RefreshToken = RefreshTokenHasher.Hash(refreshToken),
            RefreshTokenExpiryUtc = DateTime.UtcNow.AddDays(_securitySettings.RefreshTokenDays)
        }, ct);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserName} logged in from {Ip}.", user.UserName, ipAddress);
        await _audit.LogAsync(Domain.Enums.AuditAction.Login, user.UserName, $"User logged in from {ipAddress}", nameof(User), user.Id, ct);

        return ToAuthResponse(user, roleName, accessToken, refreshToken);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        // Check uniqueness
        var exists = await _uow.Users.AnyAsync(u => u.UserName == request.UserName, ct);
        if (exists)
            throw new ConflictException($"Username '{request.UserName}' is already taken.");

        // Default role or specified
        Role? role;
        if (request.RoleId.HasValue)
        {
            role = await _uow.Roles.GetByIdAsync(request.RoleId.Value, ct);
            if (role is null)
                throw new NotFoundException("Role not found.");
        }
        else
        {
            role = await _uow.Roles.FirstOrDefaultAsync(r => r.RoleType == UserRole.Viewer, ct);
        }

        var roleName = role?.Name ?? "Viewer";

        var user = new User
        {
            UserName = request.UserName,
            PasswordHash = _passwordHasher.Hash(request.Password),
            DisplayName = request.DisplayName,
            Email = request.Email,
            RoleId = role?.Id ?? 3, // Viewer default
            IsActive = true,
            IsSystem = false
        };

        await _uow.Users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        var jwtId = Guid.NewGuid().ToString("N");
        var accessToken = _tokenService.GenerateAccessToken(user, roleName, jwtId);
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Create initial session for the registered user
        await _uow.UserSessions.AddAsync(new UserSession
        {
            UserId = user.Id,
            JwtId = jwtId,
            IpAddress = null,
            UserAgent = null,
            StartedAtUtc = DateTime.UtcNow,
            IsActive = true,
            RefreshToken = RefreshTokenHasher.Hash(refreshToken),
            RefreshTokenExpiryUtc = DateTime.UtcNow.AddDays(_securitySettings.RefreshTokenDays)
        }, ct);
        
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserName} registered as {Role}.", user.UserName, roleName);
        await _audit.LogAsync(Domain.Enums.AuditAction.Create, user.UserName, $"User registered as {roleName}", nameof(User), user.Id, ct);

        return ToAuthResponse(user, roleName, accessToken, refreshToken);
    }

    public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default)
    {
        // Find session with matching hashed refresh token that isn't expired
        var hashed = RefreshTokenHasher.Hash(request.RefreshToken);
        var session = await _uow.UserSessions.FirstOrDefaultAsync(s => s.RefreshToken == hashed && s.RefreshTokenExpiryUtc > DateTime.UtcNow && s.IsActive, ct);
        
        if (session is null)
            throw new UnauthorizedException("Invalid or expired refresh token.");

        var user = await _uow.Users.FirstOrDefaultAsync(u => u.Id == session.UserId, ct, "Role");
        if (user is null || !user.IsActive)
            throw new UnauthorizedException("User is not active.");

        var roleName = user.Role?.Name ?? "Viewer";
        var jwtId = Guid.NewGuid().ToString("N");
        var accessToken = _tokenService.GenerateAccessToken(user, roleName, jwtId);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        // Rotate refresh token on the session
        session.JwtId = jwtId;
        session.RefreshToken = RefreshTokenHasher.Hash(newRefreshToken);
        session.RefreshTokenExpiryUtc = DateTime.UtcNow.AddDays(_securitySettings.RefreshTokenDays);
        _uow.UserSessions.Update(session);
        await _uow.SaveChangesAsync(ct);

        await _audit.LogAsync(Domain.Enums.AuditAction.TokenRefresh, user.UserName, "Access token refreshed.", nameof(User), user.Id, ct);

        return ToAuthResponse(user, roleName, accessToken, newRefreshToken);
    }

    public async Task LogoutAsync(int userId, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct);
        if (user is null) return;

        // Mark sessions inactive
        var sessions = await _uow.UserSessions.ListAsync(s => s.UserId == userId && s.IsActive, ct);
        foreach (var session in sessions)
        {
            session.IsActive = false;
            session.EndedAtUtc = DateTime.UtcNow;
            session.RefreshToken = null;
            session.RefreshTokenExpiryUtc = null;
            _uow.UserSessions.Update(session);
        }

        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(Domain.Enums.AuditAction.Logout, user.UserName, "User logged out.", nameof(User), user.Id, ct);
    }

    public async Task<UserDto> GetCurrentUserAsync(int userId, CancellationToken ct = default)
    {
        var user = await _uow.Users.FirstOrDefaultAsync(u => u.Id == userId, ct, "Role");
        if (user is null)
            throw new NotFoundException("User not found.");

        return new UserDto
        {
            Id = user.Id,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Role = user.Role?.RoleType ?? UserRole.Viewer,
            RoleName = user.Role?.Name ?? "Viewer",
            IsActive = user.IsActive
        };
    }

    private AuthResponse ToAuthResponse(User user, string roleName, string accessToken, string refreshToken)
        => new()
        {
            Token = accessToken,
            RefreshToken = refreshToken,
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(_securitySettings.AccessTokenMinutes),
            User = new UserDto
            {
                Id = user.Id,
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                Email = user.Email,
                Role = user.Role?.RoleType ?? UserRole.Viewer,
                RoleName = roleName,
                IsActive = user.IsActive
            }
        };
}
