using FurnaceMonitor.Application.DTOs.PartSpecification;

namespace FurnaceMonitor.Application.Interfaces.Services;

public interface IPartSpecificationService
{
    Task<List<PartSpecificationDto>> GetAllAsync(CancellationToken ct = default);
    Task<PartSpecificationDto> CreateAsync(SavePartSpecificationRequest request, CancellationToken ct = default);
    Task UpdateAsync(int id, SavePartSpecificationRequest request, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    Task DeleteAllAsync(CancellationToken ct = default);
    Task ImportFromExcelAsync(Stream excelStream, string actor, bool force = false, CancellationToken ct = default);
    Task<byte[]> ExportExcelAsync(CancellationToken ct = default);
    Task<byte[]> GenerateTemplateAsync(CancellationToken ct = default);
}
