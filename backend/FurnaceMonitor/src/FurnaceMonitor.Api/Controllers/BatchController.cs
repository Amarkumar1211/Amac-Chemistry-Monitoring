using FurnaceMonitor.Api.Extensions;
using FurnaceMonitor.Application.DTOs.Batch;
using FurnaceMonitor.Application.DTOs.Common;
using FurnaceMonitor.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurnaceMonitor.Api.Controllers;

/// <summary>
/// Operator-facing endpoints for starting, updating, clearing and closing batches.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Operator")]
public class BatchController : ControllerBase
{
    private readonly IBatchService _batch;

    public BatchController(IBatchService batch) => _batch = batch;

    /// <summary>Returns the active batch for a furnace, or null if none.</summary>
    [HttpGet("active/{furnaceId:int}")]
    [AllowAnonymous] // display screens may read without operator role
    [ProducesResponseType<ApiResponse>(200)]
    public async Task<IActionResult> GetActive(int furnaceId, CancellationToken ct)
    {
        var batch = await _batch.GetActiveBatchAsync(furnaceId, ct);
        return Ok(ApiResponse.Ok(batch));
    }

    /// <summary>Returns a batch by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<ApiResponse>(200)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var batch = await _batch.GetBatchByIdAsync(id, ct);
        return batch is null ? NotFound(ApiResponse.Fail($"Batch #{id} not found.")) : Ok(ApiResponse.Ok(batch));
    }

    /// <summary>Starts a new batch on a furnace.</summary>
    [HttpPost]
    [ProducesResponseType<ApiResponse>(200)]
    [ProducesResponseType<ApiResponse>(409)]
    public async Task<IActionResult> Create([FromBody] CreateBatchRequest request, CancellationToken ct)
    {
        var operatorId = User.UserId() ?? 0;
        var operatorName = !string.IsNullOrWhiteSpace(request.ActualOperatorName) ? request.ActualOperatorName : User.UserDisplayName();
        var result = await _batch.CreateBatchAsync(request, operatorId, operatorName, ct);
        return Ok(ApiResponse.Ok(result, "Batch started."));
    }

    /// <summary>Updates the active batch readings / indicator / instructions.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType<ApiResponse>(200)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBatchRequest request, CancellationToken ct)
    {
        var actor = !string.IsNullOrWhiteSpace(request.ActualOperatorName) ? request.ActualOperatorName : User.UserDisplayName();
        var result = await _batch.UpdateBatchAsync(id, request, actor, ct);
        return Ok(ApiResponse.Ok(result, "Batch updated."));
    }

    /// <summary>Clears a furnace, deactivating its active batch (requires confirmation).</summary>
    [HttpPost("clear/{furnaceId:int}")]
    [ProducesResponseType<ApiResponse>(200)]
    [ProducesResponseType<ApiResponse>(409)]
    public async Task<IActionResult> ClearFurnace(int furnaceId, [FromBody] ClearFurnaceRequest request, CancellationToken ct)
    {
        var actor = !string.IsNullOrWhiteSpace(request.ActualOperatorName) ? request.ActualOperatorName : User.UserDisplayName();
        await _batch.ClearFurnaceAsync(furnaceId, request.Confirm, actor, ct);
        return Ok(ApiResponse.Ok(null, "Furnace cleared."));
    }

    /// <summary>Closes a batch gracefully (requires confirmation).</summary>
    [HttpPost("close/{id:int}")]
    [ProducesResponseType<ApiResponse>(200)]
    [ProducesResponseType<ApiResponse>(409)]
    public async Task<IActionResult> CloseBatch(int id, [FromBody] CloseBatchRequest request, CancellationToken ct)
    {
        var actor = !string.IsNullOrWhiteSpace(request.ActualOperatorName) ? request.ActualOperatorName : User.UserDisplayName();
        await _batch.CloseBatchAsync(id, request.Confirm, actor, ct);
        return Ok(ApiResponse.Ok(null, "Batch closed."));
    }
}
