using FurnaceMonitor.Application.DTOs.Admin;

namespace FurnaceMonitor.Application.Interfaces.Services;

public interface IUserManagementService
{
Task<IReadOnlyList<UserManagementDto>> GetAllAsync(CancellationToken ct = default);
    Task<UserManagementDto> CreateAsync(CreateUserRequest request, string actor, CancellationToken ct = default);
    Task<UserManagementDto> UpdateAsync(int id, UpdateUserRequest request, string actor, CancellationToken ct = default);
    Task DeactivateAsync(int userId, string actor, CancellationToken ct = default);
    Task ChangePasswordAsync(int userId, ChangePasswordRequest request, string actor, CancellationToken ct = default);
}
