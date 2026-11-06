using FurnaceMonitor.Application.DTOs.Furnace;

namespace FurnaceMonitor.Application.Interfaces.Services;

public interface IFurnaceService
{
    Task<IReadOnlyList<FurnaceDto>> GetAllAsync(CancellationToken ct = default);
    Task<FurnaceDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<FurnaceDto> SaveAsync(SaveFurnaceRequest request, string actor, CancellationToken ct = default);
    Task DeleteAsync(int id, string actor, CancellationToken ct = default);
    Task HardDeleteAsync(int id, string actor, CancellationToken ct = default);
    Task<bool> NumberExistsAsync(string number, int? excludeId = null, CancellationToken ct = default);
    Task<FurnaceDto> UpdateOperationalStatusAsync(int id, string operationalStatus, string? remarks, string actor, CancellationToken ct = default);
}

