using FurnaceMonitor.Application.DTOs.Realtime;
using FurnaceMonitor.Application.Interfaces.Services;
using FurnaceMonitor.Application.Services.PdfExtraction.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace FurnaceMonitor.Infrastructure.SignalR;

/// <summary>
/// SignalR hub for real-time furnace monitoring.
/// Clients join a per-furnace group (by furnace number) and receive targeted updates.
/// </summary>
[AllowAnonymous]
public class FurnaceHub : Hub
{
    private static bool _isSystemLive = true;

    private readonly ISignalRConnectionService _connectionService;
    private readonly ILogger<FurnaceHub> _logger;

    public FurnaceHub(ISignalRConnectionService connectionService, ILogger<FurnaceHub> logger)
    {
        _connectionService = connectionService;
        _logger = logger;
    }

    /// <summary>
    /// Registers the client to receive updates for a specific furnace.
    /// </summary>
    public async Task JoinFurnace(string furnaceNumber)
    {
        if (string.IsNullOrWhiteSpace(furnaceNumber))
        {
            await Clients.Caller.SendAsync("ReceiveError", "Invalid furnace number.");
            return;
        }

        var groupName = NormalizeGroup(furnaceNumber);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        var userId = GetUserId();
        var userName = Context.User?.Identity?.Name;

        await _connectionService.RegisterConnectionAsync(Context.ConnectionId, userId, userName, furnaceNumber);

        _logger.LogInformation("Client {ConnectionId} joined group {Group}.", Context.ConnectionId, groupName);
        await Clients.Caller.SendAsync("Joined", new { FurnaceNumber = furnaceNumber, Group = groupName });
    }

    /// <summary>
    /// Removes the client from a furnace group.
    /// </summary>
    public async Task LeaveFurnace(string furnaceNumber)
    {
        if (string.IsNullOrWhiteSpace(furnaceNumber)) return;

        var groupName = NormalizeGroup(furnaceNumber);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        await _connectionService.UnregisterConnectionAsync(Context.ConnectionId);

        _logger.LogInformation("Client {ConnectionId} left group {Group}.", Context.ConnectionId, groupName);
        await Clients.Caller.SendAsync("Left", new { FurnaceNumber = furnaceNumber, Group = groupName });
    }

    /// <summary>
    /// Pushes a furnace update to a specific furnace group (used by server-side services).
    /// </summary>
    public async Task PushUpdate(string furnaceNumber, FurnaceUpdateDto update)
    {
        var groupName = NormalizeGroup(furnaceNumber);
        await Clients.Group(groupName).SendAsync("ReceiveUpdate", update);
        await Clients.All.SendAsync("ReceiveUpdate", update);
    }

    /// <summary>
    /// Pushes a furnace-cleared notification to a specific furnace group.
    /// </summary>
    public async Task PushCleared(string furnaceNumber, FurnaceClearedDto cleared)
    {
        var groupName = NormalizeGroup(furnaceNumber);
        await Clients.Group(groupName).SendAsync("ReceiveClear", cleared);
        await Clients.All.SendAsync("ReceiveClear", cleared);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("Client {ConnectionId} connected.", Context.ConnectionId);
        await Clients.Caller.SendAsync("SystemStateChanged", _isSystemLive);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Admin method to set the global system live status and broadcast to all connected displays.
    /// </summary>
    public async Task SetSystemLive(bool isLive)
    {
        _isSystemLive = isLive;
        await Clients.All.SendAsync("SystemStateChanged", isLive);
        _logger.LogInformation("System live state changed to {IsLive} by user {UserId}.", isLive, GetUserId());
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client {ConnectionId} disconnected.", Context.ConnectionId);
        await _connectionService.UnregisterConnectionAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private static string NormalizeGroup(string furnaceNumber)
        => "FURNACE:" + furnaceNumber.Trim().ToUpperInvariant();

    private int GetUserId()
    {
        var claim = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim is not null && int.TryParse(claim.Value, out var id) ? id : 0;
    }
}

/// <summary>
/// Default hub-less notification service that pushes updates through the hub.
/// Broadcasts to both the specific group AND all connected clients so that
/// plant-wide combo displays and smart TVs reliably receive all real-time data.
/// </summary>
public class RealtimeNotificationService : INotificationService
{
    private readonly IHubContext<FurnaceHub> _hub;
    private readonly ILogger<RealtimeNotificationService> _logger;

    public RealtimeNotificationService(IHubContext<FurnaceHub> hub, ILogger<RealtimeNotificationService> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task PushFurnaceUpdateAsync(FurnaceUpdateDto update, CancellationToken ct = default)
    {
        var groupName = "FURNACE:" + update.FurnaceNumber.Trim().ToUpperInvariant();
        await _hub.Clients.Group(groupName).SendAsync("ReceiveUpdate", update, ct);
        await _hub.Clients.All.SendAsync("ReceiveUpdate", update, ct);
    }

    public async Task PushFurnaceClearedAsync(FurnaceClearedDto cleared, CancellationToken ct = default)
    {
        var groupName = "FURNACE:" + cleared.FurnaceNumber.Trim().ToUpperInvariant();
        await _hub.Clients.Group(groupName).SendAsync("ReceiveClear", cleared, ct);
        await _hub.Clients.All.SendAsync("ReceiveClear", cleared, ct);
    }

    public async Task PushPdfParsedAsync(ParsedSpectroResultDto dto, CancellationToken ct = default)
    {
        await _hub.Clients.All.SendAsync("ReceivePdfParsed", dto, ct);
    }

    public async Task PushFurnaceStatusChangedAsync(FurnaceStatusChangedDto dto, CancellationToken ct = default)
    {
        var groupName = "FURNACE:" + dto.FurnaceNumber.Trim().ToUpperInvariant();
        await _hub.Clients.Group(groupName).SendAsync("ReceiveFurnaceStatusChanged", dto, ct);
        await _hub.Clients.All.SendAsync("ReceiveFurnaceStatusChanged", dto, ct);
    }
}
