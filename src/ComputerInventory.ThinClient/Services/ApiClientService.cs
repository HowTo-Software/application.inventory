using System.Net.Http.Json;
using System.Reflection;
using ComputerInventory.ThinClient.Models;

namespace ComputerInventory.ThinClient.Services;

public interface IApiClientService
{
    Task<bool> IsServerAvailableAsync();
    Task<bool> RegisterOrUpdateComputerAsync(SystemInfo systemInfo, CancellationToken cancellationToken);
    Task<HeartbeatResponse?> SendHeartbeatAsync(string serialNumber, CancellationToken cancellationToken);
    Task<bool> CheckInAsync(CheckInRequest request, CancellationToken cancellationToken);
    Task<bool> CheckOutAsync(string serialNumber, CancellationToken cancellationToken);
    Task<bool> SubmitDetailedSystemInfoAsync(string serialNumber, DetailedSystemInfo detailedInfo, CancellationToken cancellationToken);
    Task<bool> CreateRestorePointAsync(string serialNumber, int restorePointType, string description, RestorePointData data, SystemInfo systemInfo, CancellationToken cancellationToken);
}

public class HeartbeatResponse
{
    public bool ForceUpdate { get; set; }
    public bool RequestImmediateCheckin { get; set; }
    public bool ForceReinstall { get; set; }
    public bool RequiresRegistration { get; set; }
    public int HeartbeatIntervalMinutes { get; set; } = 5;
}

public class ApiClientService : IApiClientService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiClientService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ISystemInfoService _systemInfoService;
    private readonly string _apiKey;

    public ApiClientService(
        IHttpClientFactory httpClientFactory, 
        ILogger<ApiClientService> logger,
        IConfiguration configuration,
        ISystemInfoService systemInfoService)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
        _systemInfoService = systemInfoService;
        _apiKey = _configuration["ApiSettings:ApiKey"] ?? string.Empty;
    }

    private static string GetCurrentVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "1.0.0.0";
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("InventoryApi");
        if (!string.IsNullOrEmpty(_apiKey))
        {
            client.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
        }
        return client;
    }

    public async Task<bool> IsServerAvailableAsync()
    {
        try
        {
            var client = CreateClient();
            var response = await client.GetAsync("health", new CancellationTokenSource(5000).Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> RegisterOrUpdateComputerAsync(SystemInfo systemInfo, CancellationToken cancellationToken)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering computer");
            return false;
        }
    }

    public async Task<HeartbeatResponse?> SendHeartbeatAsync(string serialNumber, CancellationToken cancellationToken)
    {
        try
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
            
            // Handle 401 — API key rejected, trigger re-registration to re-authenticate
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Server returned 401 for heartbeat — invalid API key. Re-registration required.");
                return new HeartbeatResponse { RequiresRegistration = true };
            }

            // Handle 404 — server says this computer is not registered
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Server returned 404 for heartbeat — computer not registered. Re-registration required.");
                return new HeartbeatResponse { RequiresRegistration = true };
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error sending heartbeat");
            return null;
        }
    }

    public async Task<bool> CheckInAsync(CheckInRequest request, CancellationToken cancellationToken)
    {
        try
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during check-in");
            return false;
        }
    }

    public async Task<bool> CheckOutAsync(string serialNumber, CancellationToken cancellationToken)
    {
        try
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during check-out");
            return false;
        }
    }

    public async Task<bool> SubmitDetailedSystemInfoAsync(string serialNumber, DetailedSystemInfo detailedInfo, CancellationToken cancellationToken)
    {
        try
        {
            var client = CreateClient();
            
            // Build the request with ALL the data
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting detailed system info");
            return false;
        }
    }

    public async Task<bool> CreateRestorePointAsync(string serialNumber, int restorePointType, string description, RestorePointData data, SystemInfo systemInfo, CancellationToken cancellationToken)
    {
        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating restore point");
            return false;
        }
    }
}
