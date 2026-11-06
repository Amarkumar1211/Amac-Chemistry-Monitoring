using FurnaceMonitor.Application.DTOs.Batch;
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
/// Implements the batch lifecycle: create, update, clear furnace, close batch.
/// Enforces the one-active-batch-per-furnace rule and raises SignalR events.
/// </summary>
public class BatchService : IBatchService
{
    private readonly IUnitOfWork _uow;
    private readonly UnitOfWorkAccessor _accessor;
    private readonly IAuditService _audit;
    private readonly INotificationService _notification;
    private readonly ILogger<BatchService> _logger;

    public BatchService(IUnitOfWork uow, UnitOfWorkAccessor accessor, IAuditService audit, INotificationService notification, ILogger<BatchService> logger)
    {
        _uow = uow;
        _accessor = accessor;
        _audit = audit;
        _notification = notification;
        _logger = logger;
    }

    /// <inheritdoc />
    public event Func<FurnaceUpdateDto, Task>? OnFurnaceUpdated;

    /// <inheritdoc />
    public event Func<FurnaceClearedDto, Task>? OnFurnaceCleared;

    public async Task<BatchDto?> GetActiveBatchAsync(int furnaceId, CancellationToken ct = default)
    {
        var batch = await _accessor.DbContext.FurnaceBatches
            .AsNoTracking()
            .Include(b => b.Furnace)
            .Include(b => b.Operator)
            .Include(b => b.ChemicalHistory)
            .FirstOrDefaultAsync(b => b.FurnaceId == furnaceId && b.IsActive && b.Status == BatchStatus.Running, ct);

        return batch is null ? null : ToDto(batch);
    }

    public async Task<BatchDto?> GetBatchByIdAsync(int id, CancellationToken ct = default)
    {
        var batch = await _accessor.DbContext.FurnaceBatches
            .AsNoTracking()
            .Include(b => b.Furnace)
            .Include(b => b.Operator)
            .Include(b => b.ChemicalHistory)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        return batch is null ? null : ToDto(batch);
    }

    public async Task<BatchDto> CreateBatchAsync(CreateBatchRequest request, int operatorId, string operatorName, CancellationToken ct = default)
    {
        // Validate furnace exists and is active
        var furnace = await _uow.Furnaces.GetByIdAsync(request.FurnaceId, ct)
            ?? throw new NotFoundException($"Furnace #{request.FurnaceId} not found.");

        // -----------------------------------------------------------------
        // AUTO-ACTIVATE FURNACE:
        // "if Furnace is in any status if Batch will came for that particular
        // furance they do Furnace in active mode"
        // -----------------------------------------------------------------
        if (!furnace.IsActive || furnace.Status != FurnaceStatus.Active || furnace.OperationalStatus != "Melting")
        {
            furnace.IsActive = true;
            furnace.Status = FurnaceStatus.Active;
            furnace.OperationalStatus = "Melting";
            furnace.OperationalRemarks = null;
            furnace.OperationalStatusUpdatedAtUtc = DateTime.UtcNow;
            _uow.Furnaces.Update(furnace);
            await _uow.SaveChangesAsync(ct);

            await _notification.PushFurnaceStatusChangedAsync(new FurnaceStatusChangedDto
            {
                FurnaceId = furnace.Id,
                FurnaceNumber = furnace.Number,
                OperationalStatus = "Melting",
                OperationalRemarks = null,
                UpdatedAtUtc = DateTime.UtcNow
            }, ct);
        }

        // Close any previously active batch on this furnace so the new batch starts seamlessly
        var existingBatches = await _accessor.DbContext.FurnaceBatches
            .Where(b => b.FurnaceId == request.FurnaceId && b.IsActive && b.Status == BatchStatus.Running)
            .ToListAsync(ct);
        if (existingBatches.Any())
        {
            foreach (var ob in existingBatches)
            {
                ob.IsActive = false;
                ob.Status = BatchStatus.Closed;
                ob.EndedAtUtc = DateTime.UtcNow;
                ob.ClosedBy = operatorName;
                ob.ClosedTimeUtc = DateTime.UtcNow;
                ob.UpdatedBy = operatorName;
                _uow.Batches.Update(ob);
            }
            await _uow.SaveChangesAsync(ct);
        }

        var batch = new FurnaceBatch
        {
            BatchNumber = request.BatchNumber.Trim(),
            FurnaceId = request.FurnaceId,
            OperatorId = operatorId,
            Status = BatchStatus.Running,
            IsActive = true,
            StartedAtUtc = DateTime.UtcNow,
            Indicator = request.Indicator,
            Instruction = request.Instruction,
            Remarks = request.Remarks,
            ColorCode = !string.IsNullOrWhiteSpace(request.ColorCode) ? request.ColorCode : "Yellow-blue",
            CreatedBy = operatorName
        };

        await _uow.Batches.AddAsync(batch, ct);
        await _uow.SaveChangesAsync(ct);

        // Initial chemical snapshot
        var chemical = new FurnaceChemical
        {
            BatchId = batch.Id,
            Si = request.Si,
            Ti = request.Ti,
            Mg = request.Mg,
            FutureChemical1 = request.Sr ?? request.FutureChemical1,
            ColorCode = batch.ColorCode,
            Indicator = request.Indicator,
            Instruction = request.Instruction,
            Remarks = request.Remarks,
            CapturedAtUtc = DateTime.UtcNow,
            CreatedBy = operatorName
        };
        await _uow.Chemicals.AddAsync(chemical, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Batch {BatchNumber} created on furnace {Furnace} by {Operator}.", batch.BatchNumber, furnace.Number, operatorName);
        await _audit.LogAsync(AuditAction.Create, operatorName, $"Created batch {batch.BatchNumber} on furnace {furnace.Number}.", nameof(FurnaceBatch), batch.Id, ct);

        // Push initial update to subscribers
        var dto = await GetBatchByIdAsync(batch.Id, ct) ?? throw new NotFoundException("Batch could not be re-read.");
        await RaiseUpdateAsync(dto, ct);
        return dto;
    }

    public async Task<BatchDto> UpdateBatchAsync(int batchId, UpdateBatchRequest request, string actor, CancellationToken ct = default)
    {
        var batch = await _uow.Batches.FirstOrDefaultAsync(b => b.Id == batchId, ct, "Furnace", "Operator");
        if (batch is null)
            throw new NotFoundException($"Batch #{batchId} not found.");

        if (!batch.IsActive || batch.Status != BatchStatus.Running)
            throw new ConflictException("Cannot update a batch that is not active.");

        // Validate chemical limits
        ValidateChemical(request.Si, nameof(request.Si));
        ValidateChemical(request.Ti, nameof(request.Ti));
        ValidateChemical(request.Mg, nameof(request.Mg));

        // Update batch header
        if (!string.IsNullOrWhiteSpace(request.BatchNumber) && request.BatchNumber != batch.BatchNumber)
        {
            batch.BatchNumber = request.BatchNumber;
        }

        batch.Indicator = request.Indicator;
        batch.Instruction = request.Instruction;
        batch.Remarks = request.Remarks;
        if (!string.IsNullOrWhiteSpace(request.ColorCode))
        {
            batch.ColorCode = request.ColorCode;
        }
        batch.UpdatedBy = actor;
        _uow.Batches.Update(batch);

        // Update current chemical snapshot (the latest recorded snapshot for the batch)
        var chemical = await _uow.Chemicals.FirstOrDefaultAsync(c => c.BatchId == batchId, ct);
        if (chemical is null)
        {
            chemical = new FurnaceChemical
            {
                BatchId = batchId,
                Si = request.Si,
                Ti = request.Ti,
                Mg = request.Mg,
                FutureChemical1 = request.Sr ?? request.FutureChemical1,
                FutureChemical2 = request.FutureChemical2,
                FutureChemical3 = request.FutureChemical3,
                ColorCode = batch.ColorCode,
                Indicator = request.Indicator,
                Instruction = request.Instruction,
                Remarks = request.Remarks,
                CapturedAtUtc = DateTime.UtcNow,
                CreatedBy = actor
            };
            await _uow.Chemicals.AddAsync(chemical, ct);
        }
        else
        {
            chemical.Si = request.Si;
            chemical.Ti = request.Ti;
            chemical.Mg = request.Mg;
            chemical.FutureChemical1 = request.Sr ?? request.FutureChemical1 ?? chemical.FutureChemical1;
            chemical.FutureChemical2 = request.FutureChemical2;
            chemical.FutureChemical3 = request.FutureChemical3;
            chemical.ColorCode = batch.ColorCode;
            chemical.Indicator = request.Indicator;
            chemical.Instruction = request.Instruction;
            chemical.Remarks = request.Remarks;
            chemical.CapturedAtUtc = DateTime.UtcNow;
            chemical.UpdatedBy = actor;
            _uow.Chemicals.Update(chemical);
        }

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Batch {BatchNumber} updated by {Actor}.", batch.BatchNumber, actor);
        await _audit.LogAsync(AuditAction.Update, actor, $"Updated batch {batch.BatchNumber} (Si={request.Si}, Ti={request.Ti}, Mg={request.Mg}, ColorCode={batch.ColorCode}).", nameof(FurnaceBatch), batch.Id, ct);

        var dto = await GetBatchByIdAsync(batchId, ct) ?? throw new NotFoundException("Batch could not be re-read.");
        await RaiseUpdateAsync(dto, ct);
        return dto;
    }

    public async Task ClearFurnaceAsync(int furnaceId, bool confirm, string actor, CancellationToken ct = default)
    {
        if (!confirm)
            throw new ConflictException("Clear furnace requires confirmation.");

        var furnace = await _uow.Furnaces.GetByIdAsync(furnaceId, ct)
            ?? throw new NotFoundException($"Furnace #{furnaceId} not found.");

        var activeBatch = await _uow.Batches.FirstOrDefaultAsync(b => b.FurnaceId == furnaceId && b.IsActive && b.Status == BatchStatus.Running, ct);
        if (activeBatch is null)
            throw new ConflictException($"Furnace {furnace.Number} has no active batch to clear.");

        // Do NOT delete — deactivate
        activeBatch.IsActive = false;
        activeBatch.Status = BatchStatus.Cleared;
        activeBatch.EndedAtUtc = DateTime.UtcNow;
        activeBatch.ClosedBy = actor;
        activeBatch.ClosedTimeUtc = DateTime.UtcNow;
        activeBatch.UpdatedBy = actor;
        _uow.Batches.Update(activeBatch);

        furnace.OperationalStatus = "Empty";
        furnace.OperationalRemarks = null;
        furnace.OperationalStatusUpdatedAtUtc = DateTime.UtcNow;
        _uow.Furnaces.Update(furnace);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Furnace {Furnace} cleared by {Actor}.", furnace.Number, actor);
        await _audit.LogAsync(AuditAction.Clear, actor, $"Cleared furnace {furnace.Number} (batch {activeBatch.BatchNumber}).", nameof(FurnaceBatch), activeBatch.Id, ct);

        // Notify all subscribers of this furnace
        await _notification.PushFurnaceClearedAsync(new FurnaceClearedDto
        {
            FurnaceNumber = furnace.Number,
            ClearedAtUtc = DateTime.UtcNow,
            Message = "No Active Batch",
            OperationalStatus = "Empty"
        }, ct);

        await _notification.PushFurnaceStatusChangedAsync(new FurnaceStatusChangedDto
        {
            FurnaceId = furnace.Id,
            FurnaceNumber = furnace.Number,
            OperationalStatus = "Empty",
            OperationalRemarks = null,
            UpdatedAtUtc = DateTime.UtcNow
        }, ct);
    }

    public async Task CloseBatchAsync(int batchId, bool confirm, string actor, CancellationToken ct = default)
    {
        if (!confirm)
            throw new ConflictException("Close batch requires confirmation.");

        var batch = await _uow.Batches.FirstOrDefaultAsync(b => b.Id == batchId, ct, "Furnace");
        if (batch is null)
            throw new NotFoundException($"Batch #{batchId} not found.");

        if (!batch.IsActive || batch.Status != BatchStatus.Running)
            throw new ConflictException("Cannot close a batch that is not active.");

        batch.IsActive = false;
        batch.Status = BatchStatus.Closed;
        batch.EndedAtUtc = DateTime.UtcNow;
        batch.ClosedBy = actor;
        batch.ClosedTimeUtc = DateTime.UtcNow;
        batch.UpdatedBy = actor;
        _uow.Batches.Update(batch);

        if (batch.Furnace != null)
        {
            batch.Furnace.OperationalStatus = "Empty";
            batch.Furnace.OperationalRemarks = null;
            batch.Furnace.OperationalStatusUpdatedAtUtc = DateTime.UtcNow;
            _uow.Furnaces.Update(batch.Furnace);
        }

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Batch {BatchNumber} closed by {Actor}.", batch.BatchNumber, actor);
        await _audit.LogAsync(AuditAction.Close, actor, $"Closed batch {batch.BatchNumber} on furnace {batch.Furnace?.Number}.", nameof(FurnaceBatch), batch.Id, ct);

        // Notify subscribers that furnace is now empty
        await _notification.PushFurnaceClearedAsync(new FurnaceClearedDto
        {
            FurnaceNumber = batch.Furnace?.Number ?? string.Empty,
            ClearedAtUtc = DateTime.UtcNow,
            Message = "No Active Batch",
            OperationalStatus = "Empty"
        }, ct);

        if (batch.Furnace != null)
        {
            await _notification.PushFurnaceStatusChangedAsync(new FurnaceStatusChangedDto
            {
                FurnaceId = batch.Furnace.Id,
                FurnaceNumber = batch.Furnace.Number,
                OperationalStatus = "Empty",
                OperationalRemarks = null,
                UpdatedAtUtc = DateTime.UtcNow
            }, ct);
        }
    }

    private async Task RaiseUpdateAsync(BatchDto dto, CancellationToken ct)
    {
        var update = new FurnaceUpdateDto
        {
            FurnaceNumber = dto.FurnaceNumber,
            BatchNumber = dto.BatchNumber,
            Si = dto.Si,
            Ti = dto.Ti,
            Mg = dto.Mg,
            Sr = dto.Sr ?? dto.FutureChemical1,
            FutureChemical1 = dto.FutureChemical1,
            FutureChemical2 = dto.FutureChemical2,
            FutureChemical3 = dto.FutureChemical3,
            ColorCode = dto.ColorCode,
            Indicator = dto.Indicator,
            Instruction = dto.Instruction,
            Remarks = dto.Remarks,
            UpdatedAtUtc = DateTime.UtcNow
        };
        await _notification.PushFurnaceUpdateAsync(update, ct);
    }

    private static void ValidateChemical(decimal value, string field)
    {
        if (value < 0 || value > FurnaceChemical.MaxChemicalValue)
            throw new ConflictException($"{field} must be between 0 and {FurnaceChemical.MaxChemicalValue}.");
    }

    private static BatchDto ToDto(FurnaceBatch batch)
    {
        // The current chemical reading is the latest snapshot in the batch history.
        var chemical = batch.ChemicalHistory?
            .OrderByDescending(c => c.CapturedAtUtc)
            .FirstOrDefault();
        return new BatchDto
        {
            Id = batch.Id,
            BatchNumber = batch.BatchNumber,
            FurnaceId = batch.FurnaceId,
            FurnaceNumber = batch.Furnace?.Number ?? string.Empty,
            FurnaceName = batch.Furnace?.Name ?? string.Empty,
            OperatorId = batch.OperatorId,
            OperatorName = batch.CreatedBy ?? batch.Operator?.DisplayName ?? string.Empty,
            Status = batch.Status,
            IsActive = batch.IsActive,
            StartedAtUtc = batch.StartedAtUtc,
            EndedAtUtc = batch.EndedAtUtc,
            Indicator = batch.Indicator,
            Instruction = batch.Instruction,
            Remarks = batch.Remarks,
            ClosedBy = batch.ClosedBy,
            ClosedTimeUtc = batch.ClosedTimeUtc,
            ColorCode = batch.ColorCode ?? chemical?.ColorCode ?? "Yellow-blue",
            Si = chemical?.Si ?? 0m,
            Ti = chemical?.Ti ?? 0m,
            Mg = chemical?.Mg ?? 0m,
            Sr = chemical?.FutureChemical1,
            FutureChemical1 = chemical?.FutureChemical1,
            FutureChemical2 = chemical?.FutureChemical2,
            FutureChemical3 = chemical?.FutureChemical3
        };
    }
}
