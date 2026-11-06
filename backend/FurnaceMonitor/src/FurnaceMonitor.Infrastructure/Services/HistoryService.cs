using FurnaceMonitor.Application.DTOs.History;
using FurnaceMonitor.Application.Interfaces.Repositories;
using FurnaceMonitor.Application.Interfaces.Services;
using FurnaceMonitor.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FurnaceMonitor.Infrastructure.Persistence;

namespace FurnaceMonitor.Infrastructure.Services;

/// <summary>
/// Provides batch history and audit trail queries.
/// </summary>
public class HistoryService : IHistoryService
{
    private readonly UnitOfWorkAccessor _accessor;
    private readonly ILogger<HistoryService> _logger;

    public HistoryService(UnitOfWorkAccessor accessor, ILogger<HistoryService> logger)
    {
        _accessor = accessor;
        _logger = logger;
    }

    public async Task<IReadOnlyList<BatchHistoryDto>> GetBatchHistoryAsync(string? batchNumber, int? furnaceId, string? furnaceNumber, string? operatorName, DateTime? from, DateTime? to, int take = 200, CancellationToken ct = default)
    {
var query = _accessor.DbContext.FurnaceBatches
            .AsNoTracking()
            .Include(b => b.Furnace)
            .Include(b => b.Operator)
            .Include(b => b.ChemicalHistory)
            .Where(b => b.Status != BatchStatus.Running);

        if (!string.IsNullOrWhiteSpace(batchNumber))
            query = query.Where(b => b.BatchNumber.Contains(batchNumber));
        if (furnaceId.HasValue)
            query = query.Where(b => b.FurnaceId == furnaceId.Value);
        if (!string.IsNullOrWhiteSpace(furnaceNumber))
            query = query.Where(b => b.Furnace != null && b.Furnace.Number == furnaceNumber);
        if (!string.IsNullOrWhiteSpace(operatorName))
            query = query.Where(b => (b.CreatedBy != null && b.CreatedBy.Contains(operatorName)) || 
                                     (b.Operator != null && (b.Operator.DisplayName.Contains(operatorName) || b.Operator.UserName.Contains(operatorName))));
        if (from.HasValue)
            query = query.Where(b => b.StartedAtUtc >= from.Value);
        if (to.HasValue)
            query = query.Where(b => b.StartedAtUtc <= to.Value);

        var result = await query
            .OrderByDescending(b => b.StartedAtUtc)
            .Take(take)
            .ToListAsync(ct);

var dtos = result.Select(b =>
        {
            // The latest chemical snapshot represents the batch's final readings.
            var chemical = b.ChemicalHistory?
                .OrderByDescending(c => c.CapturedAtUtc)
                .FirstOrDefault();
            return new BatchHistoryDto
            {
                Id = b.Id,
                BatchNumber = b.BatchNumber,
                FurnaceNumber = b.Furnace?.Number ?? string.Empty,
                FurnaceName = b.Furnace?.Name ?? string.Empty,
                OperatorName = b.CreatedBy ?? b.Operator?.DisplayName ?? string.Empty,
                StartedAtUtc = b.StartedAtUtc,
                EndedAtUtc = b.EndedAtUtc,
                DurationMinutes = b.EndedAtUtc.HasValue && b.StartedAtUtc != default
                    ? (int)(b.EndedAtUtc.Value - b.StartedAtUtc).TotalMinutes
                    : 0,
                Status = b.Status,
                Indicator = b.Indicator,
                ClosedBy = b.ClosedBy,
                ClosedTimeUtc = b.ClosedTimeUtc,
                Si = chemical?.Si ?? 0m,
                Ti = chemical?.Ti ?? 0m,
                Mg = chemical?.Mg ?? 0m,
                Sr = chemical?.FutureChemical1 ?? 0m,
                Instruction = chemical?.Instruction,
                Remarks = chemical?.Remarks
            };
        }).ToList();

        return dtos;
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetAuditLogAsync(DateTime? from, DateTime? to, int take = 200, CancellationToken ct = default)
    {
        var query = _accessor.DbContext.AuditLogs.AsNoTracking();

        if (from.HasValue)
            query = query.Where(a => a.OccurredAtUtc >= from.Value);
        if (to.HasValue)
            query = query.Where(a => a.OccurredAtUtc <= to.Value);

        var result = await query
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(take)
            .ToListAsync(ct);

        return result.Select(a => new AuditLogDto
        {
            Id = a.Id,
            UserName = a.UserName,
            Action = a.Action,
            Description = a.Description,
            Entity = a.Entity,
            EntityId = a.EntityId,
            OccurredAtUtc = a.OccurredAtUtc
        }).ToList();
    }

    public async Task<IReadOnlyList<ChemicalConsumptionPeriodDto>> GetChemicalConsumptionAsync(DateTime from, DateTime to, string groupBy, CancellationToken ct = default)
    {
        var batches = await _accessor.DbContext.FurnaceBatches
            .AsNoTracking()
            .Include(b => b.ChemicalHistory)
            .Where(b => b.Status != BatchStatus.Running && b.StartedAtUtc >= from && b.StartedAtUtc <= to)
            .ToListAsync(ct);

        var periodGroups = batches.GroupBy(b => 
        {
            return groupBy.ToLower() switch
            {
                "day" => b.StartedAtUtc.ToString("yyyy-MM-dd"),
                "month" => b.StartedAtUtc.ToString("yyyy-MM"),
                "week" => $"{b.StartedAtUtc.Year}-W{System.Globalization.ISOWeek.GetWeekOfYear(b.StartedAtUtc):D2}",
                _ => b.StartedAtUtc.ToString("yyyy-MM-dd")
            };
        });

        var result = new List<ChemicalConsumptionPeriodDto>();

        foreach (var group in periodGroups)
        {
            var consumptions = new Dictionary<string, decimal>();

            foreach (var batch in group)
            {
                var chemical = batch.ChemicalHistory?.OrderByDescending(c => c.CapturedAtUtc).FirstOrDefault();
                if (chemical != null)
                {
                    AddConsumption(consumptions, "Si", chemical.Si);
                    AddConsumption(consumptions, "Ti", chemical.Ti);
                    AddConsumption(consumptions, "Mg", chemical.Mg);
                    if (chemical.FutureChemical1.HasValue && chemical.FutureChemical1 > 0)
                        AddConsumption(consumptions, "Sr", chemical.FutureChemical1.Value);
                }

                if (!string.IsNullOrWhiteSpace(batch.Remarks))
                {
                    try
                    {
                        var parsed = System.Text.Json.JsonDocument.Parse(batch.Remarks);
                        if (parsed.RootElement.TryGetProperty("params", out var paramsElement) && paramsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var param in paramsElement.EnumerateArray())
                            {
                                if (param.TryGetProperty("name", out var nameProp) && param.TryGetProperty("value", out var valProp))
                                {
                                    var name = nameProp.GetString();
                                    var valStr = valProp.GetString();
                                    if (!string.IsNullOrWhiteSpace(name) && decimal.TryParse(valStr, out var val) && val > 0)
                                    {
                                        AddConsumption(consumptions, name, val);
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore parse errors
                    }
                }
            }

            result.Add(new ChemicalConsumptionPeriodDto
            {
                Period = group.Key,
                Consumptions = consumptions
            });
        }

        return result.OrderBy(r => r.Period).ToList();
    }

    private static void AddConsumption(Dictionary<string, decimal> dict, string chemical, decimal value)
    {
        if (value <= 0) return;
        if (dict.ContainsKey(chemical))
            dict[chemical] += value;
        else
            dict[chemical] = value;
    }

    public async Task ResetHistoryAsync(CancellationToken ct = default)
    {
        // First delete dependent chemicals
        await _accessor.DbContext.Database.ExecuteSqlRawAsync("DELETE FROM FurnaceChemicals", ct);
        // Then delete the batches
        await _accessor.DbContext.Database.ExecuteSqlRawAsync("DELETE FROM FurnaceBatches", ct);
    }
}
