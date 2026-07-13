using System.Net.Http.Json;
using System.Reflection;
using ComputerInventory.ThinClient.Models;
using Polly;
using Polly.Retry;

namespace ComputerInventory.ThinClient.Services;

/// <summary>
/// Resilient API client with retry logic, connection monitoring, and offline caching.
/// Handles server unavailability gracefully without requiring client restart.
/// Supports automatic URL failover for clients outside the local network.
/// </summary>
public class ResilientApiClientService : IApiClientService
{
    private readonly ILogger<ResilientApiClientService> _logger;
    private readonly ILocalCacheService _cacheService;
    private readonly IConfiguration _configuration;
    private readonly ISystemInfoService _systemInfoService;
    private readonly IServerConnectionManager _connectionManager;
    private readonly string _apiKey;
    
    private bool _isServerAvailable = true;
    private bool _wasServerAvailable = true;
    private DateTime _lastServerCheck = DateTime.MinValue;
    private readonly TimeSpan _serverCheckInterval = TimeSpan.FromSeconds(30);
    
    // Retry policy with exponential backoff
    private readonly AsyncRetryPolicy _retryPolicy;
    
    // Data type constants for caching
    private const string CACHE_REGISTRATION = "registration";
    private const string CACHE_HEARTBEAT = "heartbeat";
    private const string CACHE_DETAILED_INFO = "detailed_info";

    public ResilientApiClientService(
        ILogger<ResilientApiClientService> logger,
        ILocalCacheService cacheService,
        IConfiguration configuration,
        ISystemInfoService systemInfoService,
        IServerConnectionManager connectionManager)
    {
        _logger = logger;
        _cacheService = cacheService;
        _configuration = configuration;
        _systemInfoService = systemInfoService;
        _connectionManager = connectionManager;
        _apiKey = _configuration["ApiSettings:ApiKey"] ?? string.Empty;
        
        // Configure retry policy: 3 retries with exponential backoff (2s, 4s, 8s)
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry {RetryCount} after {Delay}s due to: {Message}", 
                        retryCount, timeSpan.TotalSeconds, exception.Message);
                    // Mark current URL as potentially failed for failover
                    _connectionManager.MarkCurrentUrlFailed();
                });
    }

    private static string GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "1.0.0.0";
    }

    private HttpClient CreateClient()
    {
        // Use the connection manager to get a client with the current working URL
        return _connectionManager.CreateClient();
    }

    /// <summary>
    /// Check if server is reachable, with caching to avoid hammering.
    /// Uses ServerConnectionManager for automatic URL failover.
    /// </summary>
    public async Task<bool> IsServerAvailableAsync()
    {
        // Only check periodically
        if (DateTime.UtcNow - _lastServerCheck < _serverCheckInterval)
            return _isServerAvailable;

        try
        {
            // Use connection manager to find a working server (with failover)
            var workingUrl = await _connectionManager.FindWorkingServerAsync();
            _isServerAvailable = workingUrl != null;
            
            if (_isServerAvailable && !_wasServerAvailable)
            {
                _logger.LogInformation("Server connection restored via {Url}!", _connectionManager.CurrentServerUrl);
            }
            _wasServerAvailable = _isServerAvailable;
        }
        catch
        {
            _isServerAvailable = false;
        }
        
        _lastServerCheck = DateTime.UtcNow;
        return _isServerAvailable;
    }

    /// <summary>
    /// Process any pending cached data when server becomes available
    /// </summary>
    public async Task ProcessPendingDataAsync(CancellationToken cancellationToken)
    {
        if (!await IsServerAvailableAsync())
            return;

        // Process cached registrations
        var registrations = await _cacheService.GetPendingDataAsync<SystemInfo>(CACHE_REGISTRATION);
        foreach (var item in registrations)
        {
            if (item.Data == null) continue;
            
            try
            {
                var success = await SendRegistrationDirectAsync(item.Data, cancellationToken);
                if (success)
                {
                    await _cacheService.RemovePendingDataAsync(CACHE_REGISTRATION, item.Id);
                    _logger.LogInformation("Synced cached registration from {Time}", item.CachedAt);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync cached registration");
            }
        }

        // Process cached detailed info
        var detailedInfos = await _cacheService.GetPendingDataAsync<CachedDetailedInfo>(CACHE_DETAILED_INFO);
        foreach (var item in detailedInfos)
        {
            if (item.Data?.Info == null) continue;
            
            try
            {
                var success = await SendDetailedInfoDirectAsync(item.Data.SerialNumber, item.Data.Info, cancellationToken);
                if (success)
                {
                    await _cacheService.RemovePendingDataAsync(CACHE_DETAILED_INFO, item.Id);
                    _logger.LogInformation("Synced cached detailed info from {Time}", item.CachedAt);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync cached detailed info");
            }
        }

        var pendingReg = await _cacheService.GetPendingCountAsync(CACHE_REGISTRATION);
        var pendingDetail = await _cacheService.GetPendingCountAsync(CACHE_DETAILED_INFO);
        if (pendingReg > 0 || pendingDetail > 0)
        {
            _logger.LogInformation("Pending items - Registrations: {Reg}, Details: {Detail}", pendingReg, pendingDetail);
        }
    }

    public async Task<bool> RegisterOrUpdateComputerAsync(SystemInfo systemInfo, CancellationToken cancellationToken)
    {
        // First, try to process any pending data
        await ProcessPendingDataAsync(cancellationToken);
        
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var result = await SendRegistrationDirectAsync(systemInfo, cancellationToken);
                if (result)
                {
                    _isServerAvailable = true;
                }
                return result;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Server unavailable, caching registration data. Error: {Message}", ex.Message);
            _isServerAvailable = false;
            
            // Cache for later
            await _cacheService.SavePendingDataAsync(CACHE_REGISTRATION, systemInfo);
            return false;
        }
    }

    private async Task<bool> SendRegistrationDirectAsync(SystemInfo systemInfo, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        
        var request = new
        {
            systemInfo.SerialNumber,
            systemInfo.Hostname,
            systemInfo.IpAddress,
            systemInfo.MacAddress,
            systemInfo.Manufacturer,
            systemInfo.Model,
            systemInfo.OperatingSystem,
            systemInfo.Processor,
            systemInfo.RamGB,
            systemInfo.StorageGB,
            Type = systemInfo.ComputerType,
            Status = "Unassigned",  // Default status for new agent registrations
            systemInfo.CurrentUser,
            systemInfo.LastBootTime,
            systemInfo.IsOnline
        };

        var response = await client.PostAsJsonAsync("api/agent/register", request, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<AgentRegistrationResponse>(cancellationToken: cancellationToken);
            return result?.Success ?? false;
        }
        
        _logger.LogWarning("Registration failed with status: {Status}", response.StatusCode);
        return false;
    }

    public async Task<HeartbeatResponse?> SendHeartbeatAsync(string serialNumber, CancellationToken cancellationToken)
    {
        // Heartbeats don't need caching - they're time-sensitive
        // Just fail silently if server is unavailable
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var client = CreateClient();
                
                // Get additional system info for auto-registration if needed
                var systemInfo = await _systemInfoService.GetSystemInfoAsync();
                
                var request = new HeartbeatRequest
                {
                    SerialNumber = serialNumber,
                    Timestamp = DateTime.UtcNow,
                    CurrentUser = systemInfo.CurrentUser,
                    IsOnline = true,
                    IpAddress = systemInfo.IpAddress,
                    PublicIpAddress = systemInfo.PublicIpAddress,
                    Hostname = systemInfo.Hostname,
                    MacAddress = systemInfo.MacAddress,
                    OperatingSystem = systemInfo.OperatingSystem,
                    AgentVersion = GetCurrentVersion(),
                    // Extended fields for auto-registration
                    Manufacturer = systemInfo.Manufacturer,
                    Model = systemInfo.Model,
                    Processor = systemInfo.Processor,
                    RamGB = systemInfo.RamGB,
                    StorageGB = systemInfo.StorageGB,
                    Type = systemInfo.ComputerType,
                    // GPS Location (exact coordinates from Windows Location Services)
                    GpsLatitude = systemInfo.GpsLatitude,
                    GpsLongitude = systemInfo.GpsLongitude
                };

                var response = await client.PostAsJsonAsync("api/agent/heartbeat", request, cancellationToken);
                
                if (response.IsSuccessStatusCode)
                {
                    _isServerAvailable = true;
                    try
                    {
                        return await response.Content.ReadFromJsonAsync<HeartbeatResponse>(cancellationToken: cancellationToken);
                    }
                    catch
                    {
                        // Server might return empty OK, return default response
                        return new HeartbeatResponse();
                    }
                }
                
                // Handle 404 — server says this computer is not registered
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _isServerAvailable = true;
                    _logger.LogWarning("Server returned 404 for heartbeat — computer not registered. Re-registration required.");
                    return new HeartbeatResponse { RequiresRegistration = true };
                }
                
                return null;
            });
        }
        catch (Exception ex)
        {
            _isServerAvailable = false;
            _logger.LogDebug("Heartbeat failed (server may be offline): {Message}", ex.Message);
            return null;
        }
    }

    public async Task<bool> CheckInAsync(CheckInRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var client = CreateClient();
                
                // Create an anonymous object with Type as string for proper API serialization
                var apiRequest = new
                {
                    request.SerialNumber,
                    request.UserId,
                    request.UserName,
                    Type = request.Type.ToApiString(),  // Send string instead of enum value
                    request.Timestamp,
                    request.Notes,
                    request.Hostname,
                    request.IpAddress
                };
                
                var response = await client.PostAsJsonAsync("api/agent/checkin", apiRequest, cancellationToken);
                return response.IsSuccessStatusCode;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Check-in failed");
            return false;
        }
    }

    public async Task<bool> CheckOutAsync(string serialNumber, CancellationToken cancellationToken)
    {
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var client = CreateClient();
                
                // Send Type as string for proper API serialization
                var request = new
                {
                    SerialNumber = serialNumber,
                    Type = CheckInType.CheckOut.ToApiString(),  // "CheckOut"
                    Timestamp = DateTime.UtcNow
                };
                
                var response = await client.PostAsJsonAsync("api/agent/checkout", request, cancellationToken);
                return response.IsSuccessStatusCode;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Check-out failed");
            return false;
        }
    }

    public async Task<bool> SubmitDetailedSystemInfoAsync(string serialNumber, DetailedSystemInfo detailedInfo, CancellationToken cancellationToken)
    {
        // First, try to process any pending data
        await ProcessPendingDataAsync(cancellationToken);
        
        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                var result = await SendDetailedInfoDirectAsync(serialNumber, detailedInfo, cancellationToken);
                if (result)
                {
                    _isServerAvailable = true;
                }
                return result;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Server unavailable, caching detailed system info. Error: {Message}", ex.Message);
            _isServerAvailable = false;
            
            // Cache for later
            await _cacheService.SavePendingDataAsync(CACHE_DETAILED_INFO, new CachedDetailedInfo
            {
                SerialNumber = serialNumber,
                Info = detailedInfo
            });
            return false;
        }
    }

    private async Task<bool> SendDetailedInfoDirectAsync(string serialNumber, DetailedSystemInfo detailedInfo, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        
        var request = new
        {
            SerialNumber = serialNumber,
            CollectedAt = DateTime.UtcNow,
            MachineGuid = detailedInfo.HardwareIds.MachineGuid,
            ProductId = detailedInfo.HardwareIds.ProductId,
            Bios = detailedInfo.Bios,
            Motherboard = detailedInfo.Motherboard,
            Cpu = detailedInfo.Cpu,
            Memory = detailedInfo.Memory,
            Storage = detailedInfo.Storage,
            Gpu = detailedInfo.Gpu,
            Network = detailedInfo.Network,
            Os = detailedInfo.Os,
            Security = detailedInfo.Security,
            Battery = detailedInfo.Battery,
            Chassis = detailedInfo.Chassis,
            Software = detailedInfo.Software,
            UsersGroups = detailedInfo.UsersGroups,
            Peripherals = detailedInfo.Peripherals,
            EventLogs = detailedInfo.EventLogs,
            Performance = detailedInfo.Performance
        };

        _logger.LogDebug("Submitting detailed system info for {Serial}", serialNumber);
        var response = await client.PostAsJsonAsync("api/systemdetails/submit", request, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Successfully submitted detailed system info");
            return true;
        }
        
        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning("Failed to submit detailed system info. Status: {Status}, Error: {Error}", response.StatusCode, error);
        return false;
    }

    public async Task<bool> CreateRestorePointAsync(string serialNumber, int restorePointType, string description, RestorePointData data, SystemInfo systemInfo, CancellationToken cancellationToken)
    {
        // Check server availability first
        if (!await IsServerAvailableAsync())
        {
            _logger.LogWarning("Server unavailable - caching restore point for {Serial}", serialNumber);
            await _cacheService.SavePendingDataAsync("restore_point", serialNumber, 
                new CachedRestorePoint 
                { 
                    SerialNumber = serialNumber, 
                    RestorePointType = restorePointType,
                    Description = description,
                    Data = data, 
                    SystemInfo = systemInfo 
                });
            return false;
        }

        try
        {
            return await _retryPolicy.ExecuteAsync(async () => 
                await CreateRestorePointCoreAsync(serialNumber, restorePointType, description, data, systemInfo, cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create restore point after retries - caching for later");
            _isServerAvailable = false;
            await _cacheService.SavePendingDataAsync("restore_point", serialNumber, 
                new CachedRestorePoint 
                { 
                    SerialNumber = serialNumber, 
                    RestorePointType = restorePointType,
                    Description = description,
                    Data = data, 
                    SystemInfo = systemInfo 
                });
            return false;
        }
    }

    private async Task<bool> CreateRestorePointCoreAsync(string serialNumber, int restorePointType, string description, RestorePointData data, SystemInfo systemInfo, CancellationToken cancellationToken)
    {
        var client = CreateClient();
        
        var request = new
        {
            SerialNumber = serialNumber,
            Type = restorePointType,
            Description = description,
            SystemBootTime = data.SystemBootTime,
            UptimeSeconds = data.UptimeSeconds,
            
            // Basic info
            Hostname = systemInfo.Hostname,
            IpAddress = systemInfo.IpAddress,
            MacAddress = systemInfo.MacAddress,
            Manufacturer = systemInfo.Manufacturer,
            Model = systemInfo.Model,
            CurrentUser = systemInfo.CurrentUser,
            
            // All JSON data blobs
            data.SystemDetailsJson,
            data.InstalledSoftwareJson,
            data.RunningProcessesJson,
            data.WindowsServicesJson,
            data.StartupProgramsJson,
            data.ScheduledTasksJson,
            data.EnvironmentVariablesJson,
            data.NetworkConfigJson,
            data.StorageConfigJson,
            data.EventLogSummaryJson,
            data.WindowsUpdatesJson,
            data.DeviceDriversJson,
            data.FirewallRulesJson,
            data.LocalUsersJson,
            data.LocalGroupsJson,
            data.SharedFoldersJson,
            data.PrintersJson,
            data.UsbDevicesJson,
            data.BatteryStatusJson,
            data.BitLockerStatusJson,
            data.SecurityStatusJson
        };

        _logger.LogDebug("Creating restore point for {Serial}", serialNumber);
        var response = await client.PostAsJsonAsync("api/restorepoint", request, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Successfully created restore point for {Serial}", serialNumber);
            return true;
        }
        
        var error = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning("Failed to create restore point. Status: {Status}, Error: {Error}", response.StatusCode, error);
        return false;
    }
}

/// <summary>
/// Helper class for caching detailed info with serial number
/// </summary>
public class CachedDetailedInfo
{
    public string SerialNumber { get; set; } = string.Empty;
    public DetailedSystemInfo? Info { get; set; }
}

/// <summary>
/// Helper class for caching restore point data
/// </summary>
public class CachedRestorePoint
{
    public string SerialNumber { get; set; } = string.Empty;
    public int RestorePointType { get; set; }
    public string Description { get; set; } = string.Empty;
    public RestorePointData? Data { get; set; }
    public SystemInfo? SystemInfo { get; set; }
}
