using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ComputerInventory.ThinClient.Models;

namespace ComputerInventory.ThinClient.Services;

public interface ICheckInService
{
    Task CheckIdleStatus(CancellationToken cancellationToken);
    Task<bool> PerformCheckIn(int? userId, string? userName, CancellationToken cancellationToken);
    Task<bool> PerformCheckOut(CancellationToken cancellationToken);
}

[SupportedOSPlatform("windows")]
public class CheckInService : ICheckInService
{
    private readonly ILogger<CheckInService> _logger;
    private readonly IApiClientService _apiClient;
    private readonly ISystemInfoService _systemInfoService;
    private readonly IConfiguration _configuration;
    private DateTime _lastActivityTime = DateTime.UtcNow;
    private bool _isCheckedOut = false;

    public CheckInService(
        ILogger<CheckInService> logger,
        IApiClientService apiClient,
        ISystemInfoService systemInfoService,
        IConfiguration configuration)
    {
        _logger = logger;
        _apiClient = apiClient;
        _systemInfoService = systemInfoService;
        _configuration = configuration;
    }

    public async Task CheckIdleStatus(CancellationToken cancellationToken)
    {
        var idleTimeoutMinutes = _configuration.GetValue<int>("AgentSettings:IdleTimeoutMinutes", 30);
        var idleTime = GetIdleTime();
        
        if (idleTime.TotalMinutes >= idleTimeoutMinutes && !_isCheckedOut)
        {
            _logger.LogInformation("System idle for {Minutes} minutes, performing auto check-out", idleTime.TotalMinutes);
            await PerformAutoCheckOut(cancellationToken);
        }
        else if (idleTime.TotalMinutes < 1 && _isCheckedOut)
        {
            // User is active again — send check-in to server so status
            // changes back to Assigned and an audit log entry is created
            _logger.LogInformation("User activity detected after auto check-out, performing auto check-in");
            _isCheckedOut = false;
            _lastActivityTime = DateTime.UtcNow;
            await PerformCheckIn(null, Environment.UserName, cancellationToken);
        }
    }

    public async Task<bool> PerformCheckIn(int? userId, string? userName, CancellationToken cancellationToken)
    {
        try
        {
            var serialNumber = await _systemInfoService.GetSerialNumberAsync();
            
            var request = new CheckInRequest
            {
                SerialNumber = serialNumber,
                UserId = userId,
                UserName = userName ?? Environment.UserName,
                Type = CheckInType.CheckIn,
                Timestamp = DateTime.UtcNow
            };

            var result = await _apiClient.CheckInAsync(request, cancellationToken);
            
            if (result)
            {
                _isCheckedOut = false;
                _lastActivityTime = DateTime.UtcNow;
                _logger.LogInformation("Check-in successful for user: {User}", userName);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during check-in");
            return false;
        }
    }

    public async Task<bool> PerformCheckOut(CancellationToken cancellationToken)
    {
        try
        {
            var serialNumber = await _systemInfoService.GetSerialNumberAsync();
            var result = await _apiClient.CheckOutAsync(serialNumber, cancellationToken);
            
            if (result)
            {
                _isCheckedOut = true;
                _logger.LogInformation("Check-out successful");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during check-out");
            return false;
        }
    }

    private async Task PerformAutoCheckOut(CancellationToken cancellationToken)
    {
        try
        {
            var serialNumber = await _systemInfoService.GetSerialNumberAsync();
            
            var request = new CheckInRequest
            {
                SerialNumber = serialNumber,
                Type = CheckInType.AutoCheckOut,
                Timestamp = DateTime.UtcNow,
                Notes = "Automatic check-out due to idle timeout"
            };

            var result = await _apiClient.CheckInAsync(request, cancellationToken);
            
            if (result)
            {
                _isCheckedOut = true;
                _logger.LogInformation("Auto check-out completed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during auto check-out");
        }
    }

    private static TimeSpan GetIdleTime()
    {
        try
        {
            var lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            
            if (GetLastInputInfo(ref lastInputInfo))
            {
                // Use unchecked to handle tick count overflow correctly
                // Environment.TickCount wraps around after ~25 days, so we need to handle this
                unchecked
                {
                    // Cast to uint to match dwTime type and handle overflow correctly
                    uint currentTick = (uint)Environment.TickCount;
                    uint lastInputTick = lastInputInfo.dwTime;
                    
                    // Calculate difference handling wraparound
                    uint idleMilliseconds = currentTick - lastInputTick;
                    
                    // Sanity check: if idle time is more than 24 hours, something is wrong
                    // (likely running as SYSTEM with no user session)
                    if (idleMilliseconds > 86400000) // 24 hours in milliseconds
                    {
                        return TimeSpan.Zero; // Return zero to prevent false auto-checkout
                    }
                    
                    return TimeSpan.FromMilliseconds(idleMilliseconds);
                }
            }
        }
        catch
        {
            // If anything goes wrong (e.g., running without user session), return zero
        }
        
        return TimeSpan.Zero;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }
}
