using FurnaceMonitor.Application.DTOs.Admin;
using FurnaceMonitor.Application.Exceptions;
using FurnaceMonitor.Application.Interfaces.Repositories;
using FurnaceMonitor.Application.Interfaces.Security;
using FurnaceMonitor.Application.Interfaces.Services;
using FurnaceMonitor.Domain.Entities;
using FurnaceMonitor.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FurnaceMonitor.Infrastructure.Persistence;

namespace FurnaceMonitor.Infrastructure.Services;

/// <summary>
/// Admin-only user management: create, update, list, deactivate, change password.
/// </summary>
public class UserManagementService : IUserManagementService
{
    private readonly IUnitOfWork _uow;
    private readonly UnitOfWorkAccessor _accessor;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditService _audit;
    private readonly ILogger<UserManagementService> _logger;

    public UserManagementService(IUnitOfWork uow, UnitOfWorkAccessor accessor, IPasswordHasher passwordHasher, IAuditService audit, ILogger<UserManagementService> logger)
    {
        _uow = uow;
        _accessor = accessor;
        _passwordHasher = passwordHasher;
        _audit = audit;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UserManagementDto>> GetAllAsync(CancellationToken ct = default)
    {
        var users = await _accessor.DbContext.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .OrderBy(u => u.UserName)
            .Select(u => MapToDto(u))
            .ToListAsync(ct);

        return users;
    }

    public async Task<UserManagementDto> CreateAsync(CreateUserRequest request, string actor, CancellationToken ct = default)
    {
        var duplicate = await _uow.Users.AnyAsync(u => u.UserName == request.UserName, ct);
        if (duplicate)
            throw new ConflictException($"Username '{request.UserName}' already exists.");

        var role = await _uow.Roles.FirstOrDefaultAsync(r => r.RoleType == request.Role, ct)
            ?? await _uow.Roles.FirstOrDefaultAsync(r => r.RoleType == UserRole.Viewer, ct)
            ?? throw new NotFoundException("No matching role found.");

        var user = new User
        {
            UserName = request.UserName,
            DisplayName = request.DisplayName,
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            RoleId = role.Id,
            IsActive = request.IsActive,
            IsSystem = false
        };

        await _uow.Users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserName} created by {Actor}.", request.UserName, actor);
        await _audit.LogAsync(AuditAction.Create, actor, $"Created user {request.UserName} as {role.Name}.", nameof(User), user.Id, ct);

        return await GetByIdAsync(user.Id, ct) ?? throw new NotFoundException("User could not be re-read.");
    }

    public async Task<UserManagementDto> UpdateAsync(int userId, UpdateUserRequest request, string actor, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException($"User #{userId} not found.");

        var role = await _uow.Roles.FirstOrDefaultAsync(r => r.RoleType == request.Role, ct)
            ?? throw new NotFoundException("Role not found.");

        user.DisplayName = request.DisplayName;
        user.Email = request.Email;
        user.RoleId = role.Id;
        user.IsActive = request.IsActive;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserName} updated by {Actor}.", user.UserName, actor);
        await _audit.LogAsync(AuditAction.Update, actor, $"Updated user {user.UserName} (role {role.Name}).", nameof(User), user.Id, ct);

        return await GetByIdAsync(user.Id, ct) ?? throw new NotFoundException("User could not be re-read.");
    }

    public async Task DeactivateAsync(int userId, string actor, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException($"User #{userId} not found.");

        if (user.IsSystem)
            throw new ConflictException("System users cannot be deactivated.");

        user.IsActive = false;
        
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
        
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditAction.Update, actor, $"Deactivated user {user.UserName}.", nameof(User), user.Id, ct);
    }

    public async Task ChangePasswordAsync(int userId, ChangePasswordRequest request, string actor, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException($"User #{userId} not found.");

        // Admin reset: the target user's previous password is not required.
        _ = request.CurrentPassword; // kept for the request contract; unused on reset

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        
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
        
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditAction.Update, actor, $"Password reset for user {user.UserName} by {actor}.", nameof(User), user.Id, ct);
    }

    /// <summary>
    /// Self-service password change that verifies the caller's current password.
    /// </summary>
    public async Task ChangeOwnPasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException($"User #{userId} not found.");

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedException("Current password is incorrect.");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        
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
        
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditAction.Update, user.UserName, "Changed own password.", nameof(User), user.Id, ct);
    }

    private async Task<UserManagementDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        var user = await _accessor.DbContext.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        return user is null ? null : MapToDto(user);
    }

    private static UserManagementDto MapToDto(User user) => new()
    {
        Id = user.Id,
        UserName = user.UserName,
        DisplayName = user.DisplayName,
        Email = user.Email,
        Role = user.Role?.RoleType ?? UserRole.Viewer,
        RoleName = user.Role?.Name ?? "Viewer",
        IsActive = user.IsActive,
        IsSystem = user.IsSystem,
        LastLoginAtUtc = user.LastLoginAtUtc,
        CreatedAtUtc = user.CreatedAtUtc
    };
}
