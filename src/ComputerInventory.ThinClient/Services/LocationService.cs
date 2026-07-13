using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ComputerInventory.ThinClient.Services;

/// <summary>
/// Helper for HVCI-safe process launching with timeout protection.
/// Process.Start() can hang on HVCI systems, so we need wrapper methods.
/// </summary>
internal static class HvciSafeProcess
{
    private static bool? _isHvciEnforced;
    
    /// <summary>
    /// Check if HVCI is enforced on this system (cached result).
    /// </summary>
    public static bool IsHvciEnforced
    {
        get
        {
            if (_isHvciEnforced.HasValue)
                return _isHvciEnforced.Value;
            
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity");
                if (key != null)
                {
                    var enabled = key.GetValue("Enabled");
                    _isHvciEnforced = enabled != null && Convert.ToInt32(enabled) == 1;
                }
                else
                {
                    _isHvciEnforced = false;
                }
            }
            catch
            {
                _isHvciEnforced = false;
            }
            
            return _isHvciEnforced.Value;
        }
    }
    
    /// <summary>
    /// Start a process with timeout protection for HVCI systems.
    /// Returns null if the process couldn't be started within the timeout.
    /// </summary>
    public static async Task<Process?> StartWithTimeoutAsync(
        ProcessStartInfo startInfo, 
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        Process? process = null;
        
        try
        {
            // On HVCI systems, Process.Start itself can hang due to code integrity verification.
            // Use a task with timeout to prevent indefinite hang.
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            
            var startTask = Task.Run(() =>
            {
                try
                {
                    return Process.Start(startInfo);
                }
                catch (Exception)
                {
                    return null;
                }
            }, cts.Token);
            
            process = await startTask.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Process.Start timed out - likely HVCI hang
            return null;
        }
        catch (Exception)
        {
            return null;
        }
        
        return process;
    }
    
    /// <summary>
    /// Wait for process exit with timeout.
    /// Returns true if process exited, false if timed out.
    /// </summary>
    public static async Task<bool> WaitForExitAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            
            await process.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}

/// <summary>
/// Service for retrieving GPS location using Windows Location Services.
/// Uses PowerShell with System.Device.Location.GeoCoordinateWatcher to get exact GPS coordinates.
/// </summary>
public interface ILocationService
{
    /// <summary>
    /// Gets the current GPS coordinates using Windows Location Services.
    /// Returns null values if location services are unavailable or disabled.
    /// </summary>
    Task<GpsLocation?> GetGpsLocationAsync(int timeoutSeconds = 30);
}

/// <summary>
/// Represents GPS coordinates from Windows Location Services
/// </summary>
public class GpsLocation
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Accuracy { get; set; }  // Horizontal accuracy in meters
    public DateTime Timestamp { get; set; }
}

[SupportedOSPlatform("windows")]
public class LocationService : ILocationService
{
    private readonly ILogger<LocationService> _logger;
    private GpsLocation? _cachedLocation;
    private DateTime _cacheTime = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public LocationService(ILogger<LocationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GpsLocation?> GetGpsLocationAsync(int timeoutSeconds = 30)
    {
        // Return cached location if still valid
        if (_cachedLocation != null && DateTime.UtcNow - _cacheTime < CacheDuration)
        {
            _logger.LogDebug("Returning cached GPS location: Lat={Lat}, Lon={Lon}", 
                _cachedLocation.Latitude, _cachedLocation.Longitude);
            return _cachedLocation;
        }

        try
        {
            var location = await GetLocationFromPowerShellAsync(timeoutSeconds);
            
            if (location != null)
            {
                _cachedLocation = location;
                _cacheTime = DateTime.UtcNow;
                _logger.LogInformation("GPS location obtained: Lat={Lat}, Lon={Lon}",
                    location.Latitude, location.Longitude);
            }
            else
            {
                _logger.LogDebug("GPS location not available");
            }

            return location;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get GPS location");
            return null;
        }
    }

    /// <summary>
    /// Uses PowerShell with System.Device.Location to get GPS coordinates.
    /// This is a cross-platform compatible approach that works with Windows Location Services.
    /// </summary>
    private async Task<GpsLocation?> GetLocationFromPowerShellAsync(int timeoutSeconds)
    {
        try
        {
            // PowerShell script to get GPS location using Windows Location Services
            // Uses GeoCoordinateWatcher from System.Device.Location assembly
            var script = @"
try {
    Add-Type -AssemblyName System.Device
    $watcher = New-Object System.Device.Location.GeoCoordinateWatcher
    $watcher.Start()
    
    # Wait for location with timeout
    $timeout = [DateTime]::Now.AddSeconds(" + timeoutSeconds + @")
    while ([DateTime]::Now -lt $timeout) {
        if ($watcher.Status -eq 'Ready' -and $watcher.Position.Location.IsUnknown -eq $false) {
            $loc = $watcher.Position.Location
            $result = @{
                Latitude = $loc.Latitude
                Longitude = $loc.Longitude
                Accuracy = if ([Double]::IsNaN($loc.HorizontalAccuracy)) { $null } else { $loc.HorizontalAccuracy }
                Status = 'Success'
            }
            $watcher.Stop()
            ConvertTo-Json $result -Compress
            exit 0
        }
        if ($watcher.Status -eq 'Disabled') {
            $result = @{ Status = 'Disabled'; Error = 'Location services are disabled' }
            ConvertTo-Json $result -Compress
            exit 1
        }
        Start-Sleep -Milliseconds 500
    }
    
    $watcher.Stop()
    $result = @{ Status = 'Timeout'; Error = 'Location request timed out' }
    ConvertTo-Json $result -Compress
    exit 1
}
catch {
    $result = @{ Status = 'Error'; Error = $_.Exception.Message }
    ConvertTo-Json $result -Compress
    exit 1
}
";

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{script.Replace("\"", "\\\"")}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Use HVCI-safe process launch with timeout
            var processTimeout = TimeSpan.FromSeconds(timeoutSeconds + 10);
            using var process = await HvciSafeProcess.StartWithTimeoutAsync(startInfo, TimeSpan.FromSeconds(30));
            
            if (process == null)
            {
                _logger.LogWarning("Failed to start PowerShell process for location (possible HVCI hang)");
                return null;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            
            var completed = await HvciSafeProcess.WaitForExitAsync(process, processTimeout);

            if (!completed)
            {
                try { process.Kill(); } catch { }
                _logger.LogWarning("PowerShell location request timed out");
                return null;
            }

            var output = await outputTask;
            var error = await errorTask;

            if (!string.IsNullOrWhiteSpace(error))
            {
                _logger.LogDebug("PowerShell stderr: {Error}", error);
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                _logger.LogDebug("No output from PowerShell location script");
                return null;
            }

            // Parse JSON response
            try
            {
                using var doc = JsonDocument.Parse(output.Trim());
                var root = doc.RootElement;
                
                var status = root.GetProperty("Status").GetString();
                
                if (status == "Success")
                {
                    return new GpsLocation
                    {
                        Latitude = root.GetProperty("Latitude").GetDouble(),
                        Longitude = root.GetProperty("Longitude").GetDouble(),
                        Accuracy = root.TryGetProperty("Accuracy", out var acc) && acc.ValueKind != JsonValueKind.Null 
                            ? acc.GetDouble() 
                            : null,
                        Timestamp = DateTime.UtcNow
                    };
                }
                else
                {
                    var errorMsg = root.TryGetProperty("Error", out var err) ? err.GetString() : "Unknown error";
                    _logger.LogDebug("Location service returned: {Status} - {Error}", status, errorMsg);
                    return null;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogDebug(ex, "Failed to parse PowerShell location output: {Output}", output);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error executing PowerShell location script");
            return null;
        }
    }
}
