using System.Security.Claims;
using FurnaceMonitor.Api.Extensions;
using FurnaceMonitor.Application.DTOs.Auth;
using FurnaceMonitor.Application.DTOs.Common;
using FurnaceMonitor.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurnaceMonitor.Api.Controllers;

/// <summary>
/// Handles login, registration, token refresh and logout.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Authenticates a user and returns access + refresh tokens.</summary>
    [HttpPost("login")]
    [ProducesResponseType<ApiResponse>(200)]
    [ProducesResponseType<ApiResponse>(401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        var result = await _auth.LoginAsync(request, ip, userAgent, ct);
        return Ok(ApiResponse.Ok(result, "Login successful."));
    }

    /// <summary>Registers a new user (admin only by policy; otherwise default role).</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse>(200)]
    [ProducesResponseType<ApiResponse>(409)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _auth.RegisterAsync(request, ct);
        return Ok(ApiResponse.Ok(result, "Registration successful."));
    }

    /// <summary>Exchanges a valid refresh token for a fresh token pair.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse>(200)]
    [ProducesResponseType<ApiResponse>(401)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var result = await _auth.RefreshTokenAsync(request, ct);
        return Ok(ApiResponse.Ok(result, "Token refreshed."));
    }

    /// <summary>Revokes the current user's refresh tokens and active sessions.</summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var userId = User.UserId() ?? 0;
        await _auth.LogoutAsync(userId, ct);
        return Ok(ApiResponse.Ok(null, "Logged out."));
    }

    /// <summary>Returns the currently authenticated user's profile.</summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<ApiResponse>(200)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = User.UserId() ?? 0;
        var user = await _auth.GetCurrentUserAsync(userId, ct);
        return Ok(ApiResponse.Ok(user));
    }
}
