using FurnaceMonitor.Application.DTOs.Furnace;
using FurnaceMonitor.Application.DTOs.Realtime;
using FurnaceMonitor.Application.Exceptions;
using FurnaceMonitor.Application.Interfaces.Repositories;
using FurnaceMonitor.Application.Interfaces.Services;
using FurnaceMonitor.Domain.Entities;
using FurnaceMonitor.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FurnaceMonitor.Infrastructure.Persistence;

namespace FurnaceMonitor.Infrastructure.Services;

/// <summary>
/// Manages the furnace master records (CRUD).
/// </summary>
public class FurnaceService : IFurnaceService
{
    private readonly IUnitOfWork _uow;
    private readonly UnitOfWorkAccessor _accessor;
    private readonly IAuditService _audit;
    private readonly INotificationService _notificationService;
    private readonly ILogger<FurnaceService> _logger;

    public FurnaceService(IUnitOfWork uow, UnitOfWorkAccessor accessor, IAuditService audit, INotificationService notificationService, ILogger<FurnaceService> logger)
    {
        _uow = uow;
        _accessor = accessor;
        _audit = audit;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FurnaceDto>> GetAllAsync(CancellationToken ct = default)
    {
        var furnaces = await _accessor.DbContext.Furnaces
            .AsNoTracking()
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Number)
            .Select(f => new FurnaceDto
            {
                Id = f.Id,
                Number = f.Number,
                Name = f.Name,
                Status = f.Status,
                IsActive = f.IsActive,
                Location = f.Location,
                SortOrder = f.SortOrder,
                ActiveBatchId = f.Batches.Where(b => b.IsActive && b.Status == BatchStatus.Running).Select(b => (int?)b.Id).FirstOrDefault(),
                ActiveBatchNumber = f.Batches.Where(b => b.IsActive && b.Status == BatchStatus.Running).Select(b => b.BatchNumber).FirstOrDefault(),
                OperationalStatus = f.OperationalStatus ?? "WaitingForBatch",
                OperationalRemarks = f.OperationalRemarks,
                OperationalStatusUpdatedAtUtc = f.OperationalStatusUpdatedAtUtc
            })
            .ToListAsync(ct);

        return furnaces;
    }

    public async Task<FurnaceDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var furnace = await _accessor.DbContext.Furnaces
            .AsNoTracking()
            .Where(f => f.Id == id)
            .Select(f => new FurnaceDto
            {
                Id = f.Id,
                Number = f.Number,
                Name = f.Name,
                Status = f.Status,
                IsActive = f.IsActive,
                Location = f.Location,
                SortOrder = f.SortOrder,
                ActiveBatchId = f.Batches.Where(b => b.IsActive && b.Status == BatchStatus.Running).Select(b => (int?)b.Id).FirstOrDefault(),
                ActiveBatchNumber = f.Batches.Where(b => b.IsActive && b.Status == BatchStatus.Running).Select(b => b.BatchNumber).FirstOrDefault(),
                OperationalStatus = f.OperationalStatus ?? "WaitingForBatch",
                OperationalRemarks = f.OperationalRemarks,
                OperationalStatusUpdatedAtUtc = f.OperationalStatusUpdatedAtUtc
            })
            .FirstOrDefaultAsync(ct);

        return furnace;
    }

    public async Task<FurnaceDto> SaveAsync(SaveFurnaceRequest request, string actor, CancellationToken ct = default)
    {
        request.Number = request.Number.Trim().ToUpperInvariant();

        // Prevent any two furnace records from sharing the same number (active or not)
        if (await _uow.Furnaces.AnyAsync(f => f.Number == request.Number && f.Id != request.Id, ct))
            throw new ConflictException($"Furnace number '{request.Number}' already exists. Each furnace must have a unique number.");

        Furnace furnace;
        if (request.Id > 0)
        {
            furnace = await _uow.Furnaces.GetByIdAsync(request.Id, ct)
                ?? throw new NotFoundException($"Furnace #{request.Id} not found.");

            furnace.Number = request.Number;
            furnace.Name = request.Name.Trim();
            furnace.Status = request.Status;
            furnace.IsActive = request.IsActive;
            furnace.Location = request.Location;
            furnace.SortOrder = request.SortOrder;
            furnace.UpdatedBy = actor;
            _uow.Furnaces.Update(furnace);
            await _audit.LogAsync(AuditAction.Update, actor, $"Updated furnace {request.Number}.", nameof(Furnace), furnace.Id, ct);
        }
        else
        {
            furnace = new Furnace
            {
                Number = request.Number,
                Name = request.Name.Trim(),
                Status = request.Status,
                IsActive = request.IsActive,
                Location = request.Location,
                SortOrder = request.SortOrder,
                CreatedBy = actor
            };
            await _uow.Furnaces.AddAsync(furnace, ct);
            await _uow.SaveChangesAsync(ct); // get Id
            await _audit.LogAsync(AuditAction.Create, actor, $"Created furnace {request.Number}.", nameof(Furnace), furnace.Id, ct);
        }

        await _uow.SaveChangesAsync(ct);
        return await GetByIdAsync(furnace.Id, ct) ?? throw new NotFoundException("Furnace could not be re-read.");
    }

    public async Task DeleteAsync(int id, string actor, CancellationToken ct = default)
    {
        var furnace = await _uow.Furnaces.FirstOrDefaultAsync(f => f.Id == id, ct, "Batches");
        if (furnace is null)
            throw new NotFoundException($"Furnace #{id} not found.");

        // Prevent deleting a furnace with active batches (soft constraint)
        if (furnace.Batches.Any(b => b.IsActive))
            throw new ConflictException("Cannot delete a furnace that has an active batch. Clear the furnace first.");

        furnace.IsActive = false;
        furnace.Status = FurnaceStatus.Inactive;
        furnace.UpdatedBy = actor;
        _uow.Furnaces.Update(furnace);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditAction.Update, actor, $"Deactivated furnace {furnace.Number}.", nameof(Furnace), furnace.Id, ct);
    }

    public async Task HardDeleteAsync(int id, string actor, CancellationToken ct = default)
    {
        var furnace = await _uow.Furnaces.FirstOrDefaultAsync(f => f.Id == id, ct, "Batches");
        if (furnace is null)
            throw new NotFoundException($"Furnace #{id} not found.");

        if (furnace.Batches.Any())
            throw new ConflictException("Cannot permanently delete a furnace that has batch history. Please use Deactivate instead.");

        _uow.Furnaces.Remove(furnace);
        await _uow.SaveChangesAsync(ct);
        await _audit.LogAsync(AuditAction.Delete, actor, $"Permanently deleted furnace {furnace.Number}.", nameof(Furnace), furnace.Id, ct);
    }

    public async Task<bool> NumberExistsAsync(string number, int? excludeId = null, CancellationToken ct = default)
        => await _uow.Furnaces.AnyAsync(f => f.Number == number && (!excludeId.HasValue || f.Id != excludeId), ct);

    public async Task<FurnaceDto> UpdateOperationalStatusAsync(int id, string operationalStatus, string? remarks, string actor, CancellationToken ct = default)
    {
        var furnace = await _uow.Furnaces.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Furnace #{id} not found.");

        furnace.OperationalStatus = string.IsNullOrWhiteSpace(operationalStatus) ? "WaitingForBatch" : operationalStatus.Trim();
        furnace.OperationalRemarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
        furnace.OperationalStatusUpdatedAtUtc = DateTime.UtcNow;
        furnace.UpdatedBy = actor;

        _uow.Furnaces.Update(furnace);
        await _uow.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditAction.Update, actor, $"Changed furnace {furnace.Number} operational status to {furnace.OperationalStatus}.", nameof(Furnace), furnace.Id, ct);

        // Push real-time SignalR notification to all displays
        await _notificationService.PushFurnaceStatusChangedAsync(new FurnaceStatusChangedDto
        {
            FurnaceId = furnace.Id,
            FurnaceNumber = furnace.Number,
            OperationalStatus = furnace.OperationalStatus,
            OperationalRemarks = furnace.OperationalRemarks,
            UpdatedAtUtc = furnace.OperationalStatusUpdatedAtUtc.Value
        }, ct);

        return await GetByIdAsync(furnace.Id, ct) ?? throw new NotFoundException("Furnace could not be re-read.");
    }
}
