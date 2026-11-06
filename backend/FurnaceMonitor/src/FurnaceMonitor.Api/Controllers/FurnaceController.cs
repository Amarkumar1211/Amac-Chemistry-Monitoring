using FurnaceMonitor.Api.Extensions;
using FurnaceMonitor.Application.DTOs.Common;
using FurnaceMonitor.Application.DTOs.Furnace;
using FurnaceMonitor.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurnaceMonitor.Api.Controllers;

/// <summary>
/// Manages furnace master records (list, get, save, delete).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FurnaceController : ControllerBase
{
    private readonly IFurnaceService _furnace;

    public FurnaceController(IFurnaceService furnace) => _furnace = furnace;

    /// <summary>Returns all furnaces with their active batch summary.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<ApiResponse>(200)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var furnaces = await _furnace.GetAllAsync(ct);
        return Ok(ApiResponse.Ok(furnaces));
    }

    /// <summary>Returns a single furnace by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<ApiResponse>(200)]
    [ProducesResponseType<ApiResponse>(404)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var furnace = await _furnace.GetByIdAsync(id, ct);
        return furnace is null ? NotFound(ApiResponse.Fail($"Furnace #{id} not found.")) : Ok(ApiResponse.Ok(furnace));
    }

    /// <summary>Updates the real-time operational status of a furnace (Empty, Melting, Maintenance, Breakdown, Emergency, WaitingForBatch).</summary>
    [HttpPut("{id:int}/operational-status")]
    [Authorize]
    [ProducesResponseType<ApiResponse>(200)]
    [ProducesResponseType<ApiResponse>(404)]
    public async Task<IActionResult> UpdateOperationalStatus(int id, [FromBody] UpdateFurnaceOperationalStatusRequest request, CancellationToken ct)
    {
        var actor = User.UserDisplayName();
        var result = await _furnace.UpdateOperationalStatusAsync(id, request.OperationalStatus, request.OperationalRemarks, actor, ct);
        return Ok(ApiResponse.Ok(result, $"Furnace status updated to {request.OperationalStatus}."));
    }

    /// <summary>Creates or updates a furnace (admin).</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<ApiResponse>(200)]
    [ProducesResponseType<ApiResponse>(409)]
    public async Task<IActionResult> Save([FromBody] SaveFurnaceRequest request, CancellationToken ct)
    {
        var actor = User.UserDisplayName();
        var result = await _furnace.SaveAsync(request, actor, ct);
        return Ok(ApiResponse.Ok(result, request.Id > 0 ? "Furnace updated." : "Furnace created."));
    }

    /// <summary>Deactivates a furnace (admin).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<ApiResponse>(200)]
    [ProducesResponseType<ApiResponse>(409)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var actor = User.UserDisplayName();
        await _furnace.DeleteAsync(id, actor, ct);
        return Ok(ApiResponse.Ok(null, "Furnace deactivated."));
    }

    /// <summary>Hard deletes a furnace (admin).</summary>
    [HttpDelete("{id:int}/hard")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<ApiResponse>(200)]
    [ProducesResponseType<ApiResponse>(409)]
    public async Task<IActionResult> HardDelete(int id, CancellationToken ct)
    {
        var actor = User.UserDisplayName();
        await _furnace.HardDeleteAsync(id, actor, ct);
        return Ok(ApiResponse.Ok(null, "Furnace permanently deleted."));
    }
}
