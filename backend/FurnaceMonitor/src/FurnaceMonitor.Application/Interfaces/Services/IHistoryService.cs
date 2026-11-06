using FurnaceMonitor.Application.DTOs.History;

namespace FurnaceMonitor.Application.Interfaces.Services;

public interface IHistoryService
{
    Task<IReadOnlyList<BatchHistoryDto>> GetBatchHistoryAsync(string? batchNumber = null, int? furnaceId = null, string? furnaceNumber = null, string? operatorName = null, DateTime? from = null, DateTime? to = null, int take = 200, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLogDto>> GetAuditLogAsync(DateTime? from = null, DateTime? to = null, int take = 200, CancellationToken ct = default);
    Task<IReadOnlyList<ChemicalConsumptionPeriodDto>> GetChemicalConsumptionAsync(DateTime from, DateTime to, string groupBy, CancellationToken ct = default);
    Task ResetHistoryAsync(CancellationToken ct = default);
}

public class PagedHistoryDto
{
    public IReadOnlyList<BatchHistoryDto> Items { get; set; } = Array.Empty<BatchHistoryDto>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
