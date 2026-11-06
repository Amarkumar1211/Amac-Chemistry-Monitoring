namespace FurnaceMonitor.Application.Interfaces.Services;

/// <summary>
/// Tracks SignalR client connections and which furnaces each is subscribed to.
/// </summary>
public interface ISignalRConnectionService
{
    Task RegisterConnectionAsync(string connectionId, int userId, string? userName, string? furnaceNumber, CancellationToken ct = default);
    Task UnregisterConnectionAsync(string connectionId, CancellationToken ct = default);
    Task<int> CountActiveConnectionsAsync(CancellationToken ct = default);
    Task<int> CountFurnaceConnectionsAsync(string furnaceNumber, CancellationToken ct = default);
}
