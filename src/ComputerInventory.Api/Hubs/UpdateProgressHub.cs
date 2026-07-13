using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ComputerInventory.Api.Hubs;

/// <summary>
/// SignalR hub for broadcasting real-time agent update progress to connected Web UI clients.
/// </summary>
[Authorize]
public class UpdateProgressHub : Hub
{
    private readonly ILogger<UpdateProgressHub> _logger;

    public UpdateProgressHub(ILogger<UpdateProgressHub> logger)
    {
        _logger = logger;
    }

    public override Task OnConnectedAsync()
    {
        _logger.LogDebug("Client connected to UpdateProgressHub: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("Client disconnected from UpdateProgressHub: {ConnectionId}", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
