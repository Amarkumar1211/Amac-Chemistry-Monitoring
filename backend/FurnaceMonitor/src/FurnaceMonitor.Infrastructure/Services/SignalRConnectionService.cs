using FurnaceMonitor.Application.Interfaces.Repositories;
using FurnaceMonitor.Application.Interfaces.Services;
using FurnaceMonitor.Domain.Entities;
using FurnaceMonitor.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FurnaceMonitor.Infrastructure.Services;

/// <summary>
/// Tracks SignalR connection state for observability and reconnection support.
/// </summary>
public class SignalRConnectionService : ISignalRConnectionService
{
    private readonly IUnitOfWork _uow;
    private readonly IAuditService _audit;
    private readonly ILogger<SignalRConnectionService> _logger;

    public SignalRConnectionService(IUnitOfWork uow, IAuditService audit, ILogger<SignalRConnectionService> logger)
    {
        _uow = uow;
        _audit = audit;
        _logger = logger;
    }

    public async Task RegisterConnectionAsync(string connectionId, int userId, string? userName, string? furnaceNumber, CancellationToken ct = default)
    {
        try
        {
            var existing = await _uow.SignalRConnections.FirstOrDefaultAsync(c => c.ConnectionId == connectionId, ct);

            if (existing is not null)
            {
                existing.UserId = userId;
                existing.UserName = userName;
                existing.FurnaceNumber = furnaceNumber;
                existing.ConnectedAtUtc = DateTime.UtcNow;
                existing.IsConnected = true;
                existing.DisconnectedAtUtc = null;
                _uow.SignalRConnections.Update(existing);
                await _uow.SaveChangesAsync(ct);

                _logger.LogInformation("SignalR connection {ConnectionId} refreshed for user {User} on furnace {Furnace}.", connectionId, userName, furnaceNumber);
                await _audit.LogAsync(AuditAction.SignalRConnected, userName, $"SignalR reconnected on furnace {furnaceNumber}.", nameof(SignalRConnection), existing.Id, ct);
                return;
            }

            var conn = new SignalRConnection
            {
                ConnectionId = connectionId,
                UserId = userId,
                UserName = userName,
                FurnaceNumber = furnaceNumber,
                ConnectedAtUtc = DateTime.UtcNow,
                IsConnected = true
            };

            await _uow.SignalRConnections.AddAsync(conn, ct);
            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation("SignalR connection {ConnectionId} registered for user {User} on furnace {Furnace}.", connectionId, userName, furnaceNumber);
            await _audit.LogAsync(AuditAction.SignalRConnected, userName, $"SignalR connected on furnace {furnaceNumber}.", nameof(SignalRConnection), conn.Id, ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Concurrency issue encountered while registering SignalR connection {ConnectionId}. Connection registration ignored safely.", connectionId);
        }
    }

    public async Task UnregisterConnectionAsync(string connectionId, CancellationToken ct = default)
    {
        var conn = await _uow.SignalRConnections.FirstOrDefaultAsync(c => c.ConnectionId == connectionId && c.IsConnected, ct);
        if (conn is null) return;

        conn.IsConnected = false;
        conn.DisconnectedAtUtc = DateTime.UtcNow;
        _uow.SignalRConnections.Update(conn);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("SignalR connection {ConnectionId} unregistered.", connectionId);
        await _audit.LogAsync(AuditAction.SignalRDisconnected, conn.UserName, $"SignalR disconnected from furnace {conn.FurnaceNumber}.", nameof(SignalRConnection), conn.Id, ct);
    }

    public async Task<int> CountActiveConnectionsAsync(CancellationToken ct = default)
        => await _uow.SignalRConnections.CountAsync(c => c.IsConnected, ct);

    public async Task<int> CountFurnaceConnectionsAsync(string furnaceNumber, CancellationToken ct = default)
        => await _uow.SignalRConnections.CountAsync(c => c.IsConnected && c.FurnaceNumber == furnaceNumber, ct);
}
