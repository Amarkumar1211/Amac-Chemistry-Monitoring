using FurnaceMonitor.Application.DTOs.Common;
using FurnaceMonitor.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurnaceMonitor.Api.Controllers;

/// <summary>
/// Provides batch and audit history.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class HistoryController : ControllerBase
{
    private readonly IHistoryService _history;

    public HistoryController(IHistoryService history) => _history = history;

    /// <summary>Returns completed batch history with optional filters.</summary>
    [HttpGet("batches")]
    [ProducesResponseType<ApiResponse>(200)]
    public async Task<IActionResult> GetBatches(
        [FromQuery] string? batchNumber,
        [FromQuery] int? furnaceId,
        [FromQuery] string? furnaceNumber,
        [FromQuery] string? operatorName,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int take = 200,
        CancellationToken ct = default)
    {
        var result = await _history.GetBatchHistoryAsync(batchNumber, furnaceId, furnaceNumber, operatorName, fromUtc, toUtc, take, ct);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>Returns the audit trail (admin).</summary>
    [HttpGet("audit")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<ApiResponse>(200)]
    public async Task<IActionResult> GetAudit(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int take = 200,
        CancellationToken ct = default)
    {
        var result = await _history.GetAuditLogAsync(fromUtc, toUtc, take, ct);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>Returns aggregated chemical consumption.</summary>
    [HttpGet("consumption")]
    [ProducesResponseType<ApiResponse>(200)]
    public async Task<IActionResult> GetConsumption(
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        [FromQuery] string groupBy = "Day",
        CancellationToken ct = default)
    {
        var result = await _history.GetChemicalConsumptionAsync(fromUtc, toUtc, groupBy, ct);
        return Ok(ApiResponse.Ok(result));
    }

    /// <summary>Resets history and consumption data (Admin only).</summary>
    [HttpDelete("reset")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType<ApiResponse>(200)]
    public async Task<IActionResult> ResetHistory(CancellationToken ct = default)
    {
        await _history.ResetHistoryAsync(ct);
        return Ok(ApiResponse.Ok("History and consumption data has been reset successfully."));
    }
}
