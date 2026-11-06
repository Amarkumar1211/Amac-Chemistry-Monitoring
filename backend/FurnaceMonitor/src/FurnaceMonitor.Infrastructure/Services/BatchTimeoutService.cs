using FurnaceMonitor.Application.Interfaces.Services;
using FurnaceMonitor.Domain.Enums;
using FurnaceMonitor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FurnaceMonitor.Infrastructure.Services;

/// <summary>
/// Background service that periodically checks for batches that have been active for more than 2 hours without being closed or receiving any updates.
/// Automatically closes them to prevent stale active batches.
/// </summary>
public class BatchTimeoutService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<BatchTimeoutService> _logger;

    public BatchTimeoutService(IServiceProvider services, ILogger<BatchTimeoutService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BatchTimeoutService started. Checking for timed-out batches every 5 minutes.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var accessor = scope.ServiceProvider.GetRequiredService<UnitOfWorkAccessor>();
                var batchService = scope.ServiceProvider.GetRequiredService<IBatchService>();

                // A batch is considered timed out if it was not updated for more than 2 hours
                var cutoffTime = DateTime.UtcNow.AddHours(-2);

                // Find all active running batches older than the cutoff time based on their last update
                var timedOutBatchIds = await accessor.DbContext.FurnaceBatches
                    .Where(b => b.IsActive && b.Status == BatchStatus.Running)
                    .Where(b => (b.UpdatedAtUtc ?? b.StartedAtUtc) < cutoffTime)
                    .Select(b => b.Id)
                    .ToListAsync(stoppingToken);

                if (timedOutBatchIds.Count > 0)
                {
                    _logger.LogWarning("Found {Count} batches inactive for > 2 hours. Auto-closing...", timedOutBatchIds.Count);

                    foreach (var batchId in timedOutBatchIds)
                    {
                        try
                        {
                            // Using confirm: true because it's a system automated action
                            // System actor will log it clearly in Audit table
                            await batchService.CloseBatchAsync(batchId, confirm: true, actor: "System Timeout", ct: stoppingToken);
                            _logger.LogInformation("Successfully auto-closed timed out batch #{BatchId}.", batchId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to automatically close batch #{BatchId}.", batchId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing batch timeouts.");
            }

            // Run check every 5 minutes
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
