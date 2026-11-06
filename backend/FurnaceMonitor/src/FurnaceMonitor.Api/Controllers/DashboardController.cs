using FurnaceMonitor.Application.DTOs.Common;
using FurnaceMonitor.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurnaceMonitor.Api.Controllers;

/// <summary>
/// Provides the live operations dashboard summary.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    /// <summary>Returns aggregated live metrics: furnaces, indicators, batches, connections.</summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse>(200)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var dto = await _dashboard.GetDashboardAsync(ct);
        return Ok(ApiResponse.Ok(dto));
    }
}
