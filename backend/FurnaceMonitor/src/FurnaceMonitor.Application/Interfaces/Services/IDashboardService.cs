using FurnaceMonitor.Application.DTOs.Dashboard;

namespace FurnaceMonitor.Application.Interfaces.Services;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default);
}
