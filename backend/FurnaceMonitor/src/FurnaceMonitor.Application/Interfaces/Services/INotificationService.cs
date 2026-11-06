using FurnaceMonitor.Application.DTOs.Realtime;

using FurnaceMonitor.Application.Services.PdfExtraction.Dtos;

namespace FurnaceMonitor.Application.Interfaces.Services;

/// <summary>
/// Abstraction over the SignalR hub used to push furnace updates and
/// clear notifications to the appropriate furnace groups.
/// </summary>
public interface INotificationService
{
    Task PushFurnaceUpdateAsync(FurnaceUpdateDto update, CancellationToken ct = default);
    Task PushFurnaceClearedAsync(FurnaceClearedDto cleared, CancellationToken ct = default);
    Task PushPdfParsedAsync(ParsedSpectroResultDto dto, CancellationToken ct = default);
    Task PushFurnaceStatusChangedAsync(FurnaceStatusChangedDto dto, CancellationToken ct = default);
}
