using FurnaceMonitor.Application.DTOs.Batch;
using FurnaceMonitor.Application.DTOs.Realtime;

namespace FurnaceMonitor.Application.Interfaces.Services;

/// <summary>
/// Contract for batch lifecycle operations (create / update / clear / close)
/// plus the push notifications emitted to SignalR furnace groups.
/// </summary>
public interface IBatchService
{
    Task<BatchDto?> GetActiveBatchAsync(int furnaceId, CancellationToken ct = default);
    Task<BatchDto?> GetBatchByIdAsync(int id, CancellationToken ct = default);
    Task<BatchDto> CreateBatchAsync(CreateBatchRequest request, int operatorId, string operatorName, CancellationToken ct = default);
    Task<BatchDto> UpdateBatchAsync(int batchId, UpdateBatchRequest request, string actor, CancellationToken ct = default);
    Task ClearFurnaceAsync(int furnaceId, bool confirm, string actor, CancellationToken ct = default);
    Task CloseBatchAsync(int batchId, bool confirm, string actor, CancellationToken ct = default);

    /// <summary>Fired whenever a furnace group should receive live data.</summary>
    event Func<FurnaceUpdateDto, Task>? OnFurnaceUpdated;

    /// <summary>Fired whenever a furnace is cleared so subscribers show No Active Batch.</summary>
    event Func<FurnaceClearedDto, Task>? OnFurnaceCleared;
}
