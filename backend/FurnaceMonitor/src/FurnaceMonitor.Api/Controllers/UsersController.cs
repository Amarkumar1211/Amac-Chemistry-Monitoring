using FurnaceMonitor.Api.Extensions;
using FurnaceMonitor.Application.DTOs.Admin;
using FurnaceMonitor.Application.DTOs.Common;
using FurnaceMonitor.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurnaceMonitor.Api.Controllers;

/// <summary>
/// Admin-only user management endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IUserManagementService _users;

    public UsersController(IUserManagementService users) => _users = users;

    /// <summary>Lists all users.</summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse>(200)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _users.GetAllAsync(ct);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>Creates a user.</summary>
    [HttpPost]
    [ProducesResponseType<ApiResponse>(200)]
    [ProducesResponseType<ApiResponse>(409)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var actor = User.UserDisplayName();
        var result = await _users.CreateAsync(request, actor, ct);
        return Ok(ApiResponse.Ok(result, "User created."));
    }

    /// <summary>Updates a user's profile and role.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<ApiResponse>(200)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var actor = User.UserDisplayName();
        var result = await _users.UpdateAsync(id, request, actor, ct);
        return Ok(ApiResponse.Ok(result, "User updated."));
    }

    /// <summary>Deactivates a user.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType<ApiResponse>(200)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        var actor = User.UserDisplayName();
        await _users.DeactivateAsync(id, actor, ct);
        return Ok(ApiResponse.Ok(null, "User deactivated."));
    }

    /// <summary>Changes a user's password (admin resets).</summary>
    [HttpPost("{id:int}/password")]
    [ProducesResponseType<ApiResponse>(200)]
    public async Task<IActionResult> ChangePassword(int id, [FromBody] AdminResetPasswordRequest request, CancellationToken ct)
    {
        var actor = User.UserDisplayName();
        var change = new ChangePasswordRequest
        {
            CurrentPassword = request.AdminPassword,
            NewPassword = request.NewPassword
        };
        await _users.ChangePasswordAsync(id, change, actor, ct);
        return Ok(ApiResponse.Ok(null, "Password changed."));
    }
}

/// <summary>
/// Body for admin password reset.
/// </summary>
public class AdminResetPasswordRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    public string AdminPassword { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;
}
