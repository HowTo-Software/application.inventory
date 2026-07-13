using ComputerInventory.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace ComputerInventory.Web.Services;

/// <summary>
/// Manages the SignalR connection for receiving real-time agent update progress.
/// </summary>
public sealed class UpdateProgressHubService : IAsyncDisposable
{
    private readonly NavigationManager _navigation;
    private readonly ILogger<UpdateProgressHubService> _logger;
    private HubConnection? _hubConnection;

    public event Action<UpdateProgressMessage>? OnUpdateProgress;

    public UpdateProgressHubService(
        NavigationManager navigation,
        ILogger<UpdateProgressHubService> logger)
    {
        _navigation = navigation;
        _logger = logger;
    }

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public async Task StartAsync()
    {
        if (_hubConnection is not null)
            return;

        var hubUrl = _navigation.ToAbsoluteUri("/hubs/update-progress");

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<UpdateProgressMessage>("UpdateProgress", message =>
        {
            _logger.LogDebug(
                "Received update progress: Computer {Id} ({Hostname}) - {Status} {Progress}%",
                message.ComputerId, message.Hostname, message.UpdateStatus, message.UpdateProgress);
            OnUpdateProgress?.Invoke(message);
        });

        _hubConnection.Reconnecting += error =>
        {
            _logger.LogWarning("SignalR reconnecting: {Error}", error?.Message);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += connectionId =>
        {
            _logger.LogInformation("SignalR reconnected: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        _hubConnection.Closed += error =>
        {
            _logger.LogWarning("SignalR connection closed: {Error}", error?.Message);
            return Task.CompletedTask;
        };

        try
        {
            await _hubConnection.StartAsync();
            _logger.LogInformation("SignalR connected to UpdateProgressHub");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to connect to UpdateProgressHub — falling back to polling");
        }
    }

    public async Task StopAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }
}
