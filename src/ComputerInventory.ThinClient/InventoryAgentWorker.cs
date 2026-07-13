using System.Diagnostics;
using System.Runtime.Versioning;
using ComputerInventory.ThinClient.Services;

namespace ComputerInventory.ThinClient;

[SupportedOSPlatform("windows")]
public class InventoryAgentWorker : BackgroundService
{
    private readonly ILogger<InventoryAgentWorker> _logger;
    private readonly ISystemInfoService _systemInfoService;
    private readonly IApiClientService _apiClient;
    private readonly ICheckInService _checkInService;
    private readonly IUpdateService _updateService;
    private readonly DetailedSystemCollector _detailedCollector;
    private readonly RestorePointCollector _restorePointCollector;
    private readonly IConfiguration _configuration;
    
    private TimeSpan _heartbeatInterval;
    private readonly TimeSpan _checkInInterval;
    private readonly TimeSpan _detailedReportInterval;
    private readonly TimeSpan _updateCheckInterval;
    
    // Update synchronization
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private bool _updateInProgress = false;
    private DateTime? _lastUpdateAttempt = null;
    private int _consecutiveUpdateFailures = 0;
    private const int MaxConsecutiveFailures = 3;
    private readonly int _lastResortFailureThreshold;
    private readonly TimeSpan _lastResortCooldown;
    private static readonly TimeSpan UpdateCooldown = TimeSpan.FromMinutes(5);
    private DateTime? _lastReinstallAttempt = null;
    private readonly bool _enableLastResortReinstall;

    public InventoryAgentWorker(
        ILogger<InventoryAgentWorker> logger,
        ISystemInfoService systemInfoService,
        IApiClientService apiClient,
        ICheckInService checkInService,
        IUpdateService updateService,
        DetailedSystemCollector detailedCollector,
        RestorePointCollector restorePointCollector,
        IConfiguration configuration)
    {
        _logger = logger;
        _systemInfoService = systemInfoService;
        _apiClient = apiClient;
        _checkInService = checkInService;
        _updateService = updateService;
        _detailedCollector = detailedCollector;
        _restorePointCollector = restorePointCollector;
        _configuration = configuration;
        
        var heartbeatMinutes = _configuration.GetValue<int>("AgentSettings:HeartbeatIntervalMinutes", 5);
        var checkInMinutes = _configuration.GetValue<int>("AgentSettings:CheckInIntervalMinutes", 15);
        var detailedReportMinutes = _configuration.GetValue<int>("AgentSettings:DetailedReportIntervalMinutes", 60);
        var updateCheckMinutes = _configuration.GetValue<int>("AgentSettings:UpdateCheckIntervalMinutes", 60);
        
        _heartbeatInterval = TimeSpan.FromMinutes(heartbeatMinutes);
        _checkInInterval = TimeSpan.FromMinutes(checkInMinutes);
        _detailedReportInterval = TimeSpan.FromMinutes(detailedReportMinutes);
        _updateCheckInterval = TimeSpan.FromMinutes(updateCheckMinutes);
        
        // Last resort reinstall settings
        _enableLastResortReinstall = _configuration.GetValue<bool>("AgentSettings:EnableLastResortReinstall", true);
        _lastResortFailureThreshold = _configuration.GetValue<int>("AgentSettings:LastResortFailureThreshold", 5);
        var cooldownHours = _configuration.GetValue<int>("AgentSettings:LastResortCooldownHours", 1);
        _lastResortCooldown = TimeSpan.FromHours(cooldownHours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Computer Inventory Agent started at: {time}", DateTimeOffset.Now);

        // Wait for network to be ready on boot (network services may not be available immediately)
        await WaitForNetworkAndServerAsync(stoppingToken);

        // Initial registration/check-in with retry loop
        await PerformInitialRegistrationWithRetry(stoppingToken);
        
        // Create a restore point on startup (reboot backup)
        await CreateRebootRestorePoint(stoppingToken);
        
        // Initial detailed report
        await SubmitDetailedReport(stoppingToken);

        var lastCheckIn = DateTime.UtcNow;
        var lastDetailedReport = DateTime.UtcNow;
        var lastUpdateCheck = DateTime.UtcNow;
        var hasCheckedForUpdates = false;  // Track if we've successfully checked for updates
        
        // Check for updates on startup (after initial registration)
        hasCheckedForUpdates = await TryCheckForUpdatesAsync(stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Skip heartbeats if update is in progress
                if (_updateInProgress)
                {
                    _logger.LogDebug("Update in progress, skipping heartbeat cycle...");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }
                
                // Send heartbeat
                var (heartbeatSuccess, forceUpdate, requestImmediateCheckin, forceReinstall) = await SendHeartbeatAsync(stoppingToken);
                
                // If server requested force reinstall, do it immediately (nuclear option)
                if (forceReinstall)
                {
                    _logger.LogWarning("🔴 SERVER REQUESTED FORCE REINSTALL - Triggering last resort reinstall...");
                    await TriggerLastResortReinstallAsync(stoppingToken, bypassCooldown: true);
                    continue;
                }
                
                // If server requested force update, do it immediately (with protection)
                if (forceUpdate && !_updateInProgress)
                {
                    var shouldTryUpdate = true;
                    
                    // Check if we're in a cooldown period after failures
                    if (_consecutiveUpdateFailures >= MaxConsecutiveFailures)
                    {
                        if (_lastUpdateAttempt.HasValue && DateTime.UtcNow - _lastUpdateAttempt.Value < UpdateCooldown)
                        {
                            var remaining = UpdateCooldown - (DateTime.UtcNow - _lastUpdateAttempt.Value);
                            _logger.LogWarning("⚠️ Update cooldown active after {Failures} failures. Retry in {Minutes:F1} minutes.",
                                _consecutiveUpdateFailures, remaining.TotalMinutes);
                            shouldTryUpdate = false;
                            
                            // If we've hit cooldown on force update, trigger last resort immediately
                            if (_enableLastResortReinstall)
                            {
                                _logger.LogWarning("⚠️ Force update stuck in cooldown - triggering last resort reinstall as failsafe...");
                                await TriggerLastResortReinstallAsync(stoppingToken, bypassCooldown: false);
                            }
                        }
                        else
                        {
                            // Cooldown expired, reset failure count and try again
                            _logger.LogInformation("Update cooldown expired, resetting failure counter...");
                            _consecutiveUpdateFailures = 0;
                        }
                    }
                    
                    if (shouldTryUpdate)
                    {
                        _logger.LogWarning("⚡ SERVER REQUESTED FORCE UPDATE - Triggering immediate update check...");
                        var updateSuccess = await TryCheckForUpdatesAsync(stoppingToken, isForced: true);
                        
                        if (updateSuccess)
                        {
                            _consecutiveUpdateFailures = 0;
                            _logger.LogInformation("✅ Force update initiated successfully");
                        }
                        else
                        {
                            _consecutiveUpdateFailures++;
                            _lastUpdateAttempt = DateTime.UtcNow;
                            _logger.LogWarning("❌ Force update FAILED (attempt {Count}/{Max})", 
                                _consecutiveUpdateFailures, MaxConsecutiveFailures);
                            
                            // Check if we should trigger last resort reinstall
                            if (_enableLastResortReinstall && _consecutiveUpdateFailures >= _lastResortFailureThreshold)
                            {
                                await TriggerLastResortReinstallAsync(stoppingToken, bypassCooldown: false);
                            }
                        }
                    }
                }
                
                // If server requested immediate check-in, send another heartbeat right away
                if (requestImmediateCheckin)
                {
                    _logger.LogInformation("Server requested immediate check-in - sending heartbeat now...");
                    await Task.Delay(1000, stoppingToken); // Small delay to let server process
                    await SendHeartbeatAsync(stoppingToken);
                }
                
                // If we haven't successfully checked for updates yet and heartbeat succeeded, try now
                if (!hasCheckedForUpdates && heartbeatSuccess)
                {
                    _logger.LogInformation("First successful connection - checking for updates...");
                    hasCheckedForUpdates = await TryCheckForUpdatesAsync(stoppingToken);
                }
                
                // Full check-in at intervals
                if (DateTime.UtcNow - lastCheckIn >= _checkInInterval)
                {
                    await PerformFullCheckIn(stoppingToken);
                    lastCheckIn = DateTime.UtcNow;
                }
                
                // Detailed report at intervals
                if (DateTime.UtcNow - lastDetailedReport >= _detailedReportInterval)
                {
                    await SubmitDetailedReport(stoppingToken);
                    lastDetailedReport = DateTime.UtcNow;
                }
                
                // Check for updates at intervals
                if (DateTime.UtcNow - lastUpdateCheck >= _updateCheckInterval)
                {
                    await TryCheckForUpdatesAsync(stoppingToken);
                    lastUpdateCheck = DateTime.UtcNow;
                }
                
                // Check for idle timeout (auto check-out)
                if (_configuration.GetValue<bool>("AgentSettings:EnableAutoCheckOut", true))
                {
                    await _checkInService.CheckIdleStatus(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during agent cycle");
            }

            await Task.Delay(_heartbeatInterval, stoppingToken);
        }
        
        _logger.LogInformation("Computer Inventory Agent stopping at: {time}", DateTimeOffset.Now);
    }

    /// <summary>
    /// Wait for network connectivity and server availability on boot.
    /// Network services may not be ready immediately after system startup.
    /// </summary>
    private async Task WaitForNetworkAndServerAsync(CancellationToken cancellationToken)
    {
        var startupDelaySeconds = _configuration.GetValue<int>("AgentSettings:StartupDelaySeconds", 10);
        var maxWaitMinutes = _configuration.GetValue<int>("AgentSettings:MaxNetworkWaitMinutes", 5);
        var maxWaitTime = TimeSpan.FromMinutes(maxWaitMinutes);
        var startTime = DateTime.UtcNow;

        // Initial delay to let system services start
        _logger.LogInformation("Waiting {Delay} seconds for system services to initialize...", startupDelaySeconds);
        await Task.Delay(TimeSpan.FromSeconds(startupDelaySeconds), cancellationToken);

        _logger.LogInformation("Checking network and server availability...");

        while (!cancellationToken.IsCancellationRequested)
        {
            // Check if we've exceeded max wait time
            if (DateTime.UtcNow - startTime > maxWaitTime)
            {
                _logger.LogWarning("Network/server not available after {Minutes} minutes. Continuing with offline mode.", maxWaitMinutes);
                break;
            }

            // Check network connectivity
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                _logger.LogDebug("Network not available yet, waiting...");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                continue;
            }

            // Check server availability
            if (await _apiClient.IsServerAvailableAsync())
            {
                _logger.LogInformation("Network and server are available. Proceeding with registration.");
                return;
            }

            _logger.LogDebug("Server not reachable yet, retrying in 5 seconds...");
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }
    }

    /// <summary>
    /// Perform initial registration with retry logic for boot scenarios.
    /// Keeps retrying until successful or cancelled.
    /// </summary>
    private async Task PerformInitialRegistrationWithRetry(CancellationToken cancellationToken)
    {
        var maxRetries = _configuration.GetValue<int>("AgentSettings:MaxRegistrationRetries", 10);
        var retryDelaySeconds = _configuration.GetValue<int>("AgentSettings:RegistrationRetryDelaySeconds", 30);
        var attempt = 0;

        while (!cancellationToken.IsCancellationRequested && attempt < maxRetries)
        {
            attempt++;
            _logger.LogInformation("Initial registration attempt {Attempt} of {Max}...", attempt, maxRetries);

            try
            {
                var systemInfo = await _systemInfoService.GetSystemInfoAsync();
                var result = await _apiClient.RegisterOrUpdateComputerAsync(systemInfo, cancellationToken);

                if (result)
                {
                    _logger.LogInformation("✓ Successfully registered with server. Serial: {Serial}", systemInfo.SerialNumber);
                    return; // Success - exit the retry loop
                }
                
                _logger.LogWarning("Registration returned failure. Retrying in {Delay} seconds...", retryDelaySeconds);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Registration attempt {Attempt} failed: {Message}", attempt, ex.Message);
            }

            if (attempt < maxRetries)
            {
                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds), cancellationToken);
            }
        }

        _logger.LogError("Failed to register after {Attempts} attempts. Will continue with heartbeat cycle.", attempt);
    }

    private async Task<(bool Success, bool ForceUpdate, bool RequestImmediateCheckin, bool ForceReinstall)> SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        try
        {
            var serialNumber = await _systemInfoService.GetSerialNumberAsync();
            var response = await _apiClient.SendHeartbeatAsync(serialNumber, cancellationToken);
            if (response != null)
            {
                // Server says this computer isn't registered — re-register
                if (response.RequiresRegistration)
                {
                    _logger.LogWarning("Server requires registration — triggering re-registration...");
                    await PerformInitialRegistrationWithRetry(cancellationToken);
                    return (false, false, false, false);
                }

                _logger.LogDebug("Heartbeat sent successfully. ForceUpdate={ForceUpdate}, RequestImmediateCheckin={RequestImmediateCheckin}, ForceReinstall={ForceReinstall}", 
                    response.ForceUpdate, response.RequestImmediateCheckin, response.ForceReinstall);
                
                // Apply server-configured heartbeat interval
                if (response.HeartbeatIntervalMinutes > 0)
                {
                    var serverInterval = TimeSpan.FromMinutes(response.HeartbeatIntervalMinutes);
                    if (serverInterval != _heartbeatInterval)
                    {
                        _logger.LogInformation("Heartbeat interval changed from {Old} to {New} minutes (server-configured)",
                            _heartbeatInterval.TotalMinutes, response.HeartbeatIntervalMinutes);
                        _heartbeatInterval = serverInterval;
                    }
                }
                
                return (true, response.ForceUpdate, response.RequestImmediateCheckin, response.ForceReinstall);
            }
            return (false, false, false, false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send heartbeat");
            return (false, false, false, false);
        }
    }

    private async Task<bool> TryCheckForUpdatesAsync(CancellationToken cancellationToken, bool isForced = false)
    {
        // Try to acquire update lock with timeout
        if (!await _updateLock.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken))
        {
            _logger.LogWarning("Update already in progress, skipping this check");
            return false;
        }
        
        try
        {
            _updateInProgress = true;
            _logger.LogInformation(">>> Starting update check (forced={IsForced})...", isForced);
            
            var success = await CheckForUpdatesAsync(cancellationToken, isForced);
            
            if (success)
            {
                _logger.LogInformation("<<< Update initiated - service will restart");
                return true;
            }
            else
            {
                _logger.LogInformation("<<< Update check completed (no update needed or available)");
                return true; // Check completed successfully even if no update
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "<<< Update check FAILED: {Message}", ex.Message);
            return false;
        }
        finally
        {
            _updateInProgress = false;
            _updateLock.Release();
        }
    }

    private async Task PerformFullCheckIn(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Performing full check-in...");
        
        try
        {
            var systemInfo = await _systemInfoService.GetSystemInfoAsync();
            
            // Check for hardware changes
            if (_configuration.GetValue<bool>("AgentSettings:ReportHardwareChanges", true))
            {
                var hasChanges = await _systemInfoService.DetectHardwareChangesAsync();
                if (hasChanges)
                {
                    _logger.LogInformation("Hardware changes detected, updating server");
                }
            }
            
            // Update registration data (hardware, IP, etc.)
            await _apiClient.RegisterOrUpdateComputerAsync(systemInfo, cancellationToken);
            
            // Perform actual user check-in so the server logs an audit entry
            // and updates the computer status to Assigned
            await _checkInService.PerformCheckIn(null, systemInfo.CurrentUser, cancellationToken);
            
            _logger.LogInformation("Full check-in completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during full check-in");
        }
    }

    private async Task SubmitDetailedReport(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Collecting DETAILED system information (VERBOSE MODE)...");
        
        try
        {
            var serialNumber = await _systemInfoService.GetSerialNumberAsync();
            _logger.LogInformation("Gathering ALL system data for {Serial}...", serialNumber);
            
            var detailedInfo = await _detailedCollector.CollectAllAsync();
            
            _logger.LogInformation("Collected: {Software} software, {Updates} updates, {Processes} processes, {Services} services",
                detailedInfo.Software.InstalledSoftwareCount ?? 0,
                detailedInfo.Software.InstalledUpdatesCount ?? 0,
                detailedInfo.Software.RunningProcessesCount ?? 0,
                (detailedInfo.Software.RunningServicesCount ?? 0) + (detailedInfo.Software.StoppedServicesCount ?? 0));
            
            await _apiClient.SubmitDetailedSystemInfoAsync(serialNumber, detailedInfo, cancellationToken);
            _logger.LogInformation("Detailed system report submitted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting detailed system report");
        }
    }

    /// <summary>
    /// Create a restore point backup on every reboot/startup
    /// This saves EVERYTHING to SQL Server for disaster recovery
    /// </summary>
    private async Task CreateRebootRestorePoint(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating REBOOT RESTORE POINT - backing up complete system state to SQL Server...");
        
        try
        {
            var systemInfo = await _systemInfoService.GetSystemInfoAsync();
            _logger.LogInformation("Collecting all system data for restore point...");
            
            // Collect all restore point data
            var restoreData = await _restorePointCollector.CollectRestorePointDataAsync();
            
            _logger.LogInformation("Sending restore point to server...");
            
            var success = await _apiClient.CreateRestorePointAsync(
                systemInfo.SerialNumber,
                0, // RestorePointType.Reboot
                $"System startup at {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                restoreData,
                systemInfo,
                cancellationToken);
            
            if (success)
            {
                _logger.LogInformation("✓ RESTORE POINT CREATED - System state backed up to SQL Server successfully");
            }
            else
            {
                _logger.LogWarning("Failed to create restore point on server - will retry next heartbeat");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating reboot restore point");
        }
    }

    /// <summary>
    /// Check for agent updates from the server and apply if available.
    /// Updates are applied via an external updater process for resilience.
    /// Returns true if update was initiated, false if no update needed or check failed.
    /// </summary>
    private async Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken, bool isForced = false)
    {
        if (!_configuration.GetValue<bool>("AgentSettings:EnableAutoUpdate", true))
        {
            _logger.LogDebug("Auto-update is disabled in configuration");
            return false;
        }

        try
        {
            _logger.LogInformation("Checking for agent updates (forced={IsForced})...", isForced);
            
            var updateInfo = await _updateService.CheckForUpdateAsync(cancellationToken);
            
            if (updateInfo == null)
            {
                _logger.LogInformation("No updates available. Current version: {Version}", _updateService.GetCurrentVersion());
                return false;
            }

            _logger.LogInformation("🔄 Update available: {CurrentVersion} -> {NewVersion}", 
                _updateService.GetCurrentVersion(), updateInfo.Version);

            if (updateInfo.IsBreakingChange)
            {
                _logger.LogWarning("⚠️ This is a BREAKING CHANGE update. Agent will restart after update.");
            }

            // Apply the update (this will launch the updater and the service will be restarted)
            var success = await _updateService.DownloadAndApplyUpdateAsync(updateInfo, cancellationToken);
            
            if (success)
            {
                _logger.LogInformation("✅ Update initiated successfully. Service will restart with new version.");
                // The updater will stop this service, so we just wait for that to happen
                // Give the updater time to start before we continue
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                return true;
            }
            else
            {
                _logger.LogWarning("❌ Failed to apply update. Will retry on next check.");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates");
            return false;
        }
    }

    /// <summary>
    /// Last resort reinstall mechanism. Triggered after multiple consecutive update failures
    /// or when the server requests a force reinstall.
    /// Downloads and executes the install script from the server to perform a full reinstall.
    /// This is a nuclear option that should fix most agent issues.
    /// </summary>
    private async Task TriggerLastResortReinstallAsync(CancellationToken cancellationToken, bool bypassCooldown = false)
    {
        // Check cooldown to prevent reinstall loops (unless bypassed by server request)
        if (!bypassCooldown && _lastReinstallAttempt.HasValue && DateTime.UtcNow - _lastReinstallAttempt.Value < _lastResortCooldown)
        {
            var remaining = _lastResortCooldown - (DateTime.UtcNow - _lastReinstallAttempt.Value);
            _logger.LogWarning("🚫 Reinstall cooldown active. Next attempt in {Hours:F1} hours.", remaining.TotalHours);
            return;
        }

        _lastReinstallAttempt = DateTime.UtcNow;
        _logger.LogWarning("🆘 LAST RESORT: Triggering full agent reinstall after {Count} consecutive failures (bypassCooldown={Bypass})...", 
            _consecutiveUpdateFailures, bypassCooldown);

        try
        {
            // Get the server base URL from configuration
            var baseUrl = _configuration["ApiSettings:BaseUrl"]?.TrimEnd('/');
            if (string.IsNullOrEmpty(baseUrl))
            {
                _logger.LogError("Cannot trigger reinstall: No server URL configured");
                return;
            }

            var installScriptUrl = $"{baseUrl}/api/agent/download/install-script";
            _logger.LogInformation("Will download install script from: {Url}", installScriptUrl);

            // Create a PowerShell script that stops the service, downloads and executes the install script
            var reinstallScript = $@"
# Computer Inventory Agent - Last Resort Reinstall
# Triggered automatically after multiple update failures
$ErrorActionPreference = 'Continue'

# Log to file for debugging
$logPath = Join-Path $env:ProgramData 'ComputerInventory\reinstall.log'
$logDir = Split-Path $logPath -Parent
if (-not (Test-Path $logDir)) {{ New-Item -ItemType Directory -Path $logDir -Force | Out-Null }}

function Log {{ param($msg) $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'; ""$timestamp - $msg"" | Out-File $logPath -Append -Encoding UTF8; Write-Host $msg }}

Log '========================================'
Log ' LAST RESORT REINSTALL TRIGGERED'
Log '========================================'
Log ''
Log 'Previous update attempts failed. Performing full reinstall...'
Log 'Server URL: {baseUrl}'

# Stop the current service first
Log 'Stopping ComputerInventoryAgent service...'
try {{
    Stop-Service -Name 'ComputerInventoryAgent' -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 3
}} catch {{
    Log ""Warning: Could not stop service: $_""
}}

# Download and execute the install script
try {{
    Log 'Downloading install script from server...'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $installScript = Invoke-RestMethod -Uri '{installScriptUrl}' -TimeoutSec 60
    
    Log 'Executing install script...'
    Invoke-Expression $installScript
    
    Log ''
    Log 'Reinstall completed successfully!'
}} catch {{
    Log ""ERROR: Failed to reinstall: $_""
    
    # Write to event log
    try {{
        $source = 'ComputerInventoryAgent'
        if (-not [System.Diagnostics.EventLog]::SourceExists($source)) {{
            [System.Diagnostics.EventLog]::CreateEventSource($source, 'Application')
        }}
        [System.Diagnostics.EventLog]::WriteEntry($source, ""Last resort reinstall failed: $_"", 'Error')
    }} catch {{ }}
    
    # Try to restart the existing service if reinstall failed
    Log 'Attempting to restart existing service...'
    Start-Service -Name 'ComputerInventoryAgent' -ErrorAction SilentlyContinue
    
    exit 1
}}
";

            // Save the reinstall script to ProgramData (persists and accessible by SYSTEM)
            var scriptDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ComputerInventory");
            Directory.CreateDirectory(scriptDir);
            var tempScriptPath = Path.Combine(scriptDir, "LastResortReinstall.ps1");
            await File.WriteAllTextAsync(tempScriptPath, reinstallScript, cancellationToken);
            
            _logger.LogInformation("Saved reinstall script to: {Path}", tempScriptPath);

            // Create a scheduled task to run the reinstall (works better from a service context)
            var taskName = "ComputerInventoryReinstall";
            var createTaskCmd = $@"schtasks /Create /TN ""{taskName}"" /TR ""powershell.exe -ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File '{tempScriptPath}'"" /SC ONCE /ST {DateTime.Now.AddSeconds(10):HH:mm} /F /RL HIGHEST /RU SYSTEM";
            
            _logger.LogInformation("Creating scheduled task for reinstall...");
            
            // Use HVCI-safe process launch with timeout to prevent hangs on HVCI systems
            var createProcessInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {createTaskCmd}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            
            using var createProcess = await Services.HvciSafeProcess.StartWithTimeoutAsync(
                createProcessInfo, TimeSpan.FromSeconds(30), cancellationToken);
            
            if (createProcess != null)
            {
                var outputTask = createProcess.StandardOutput.ReadToEndAsync(cancellationToken);
                var errorTask = createProcess.StandardError.ReadToEndAsync(cancellationToken);
                
                var exited = await Services.HvciSafeProcess.WaitForExitAsync(createProcess, TimeSpan.FromSeconds(30), cancellationToken);
                if (!exited)
                {
                    try { createProcess.Kill(); } catch { }
                    _logger.LogWarning("Scheduled task creation timed out");
                }
                
                var output = await outputTask;
                var error = await errorTask;
                
                if (createProcess.ExitCode == 0)
                {
                    _logger.LogWarning("🔄 Scheduled reinstall task created successfully. Reinstall will begin in ~10 seconds...");
                    
                    // Run the task immediately as well (belt and suspenders)
                    var runTaskCmd = $@"schtasks /Run /TN ""{taskName}""";
                    var runProcessInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c {runTaskCmd}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    
                    // Fire and forget - don't wait for this
                    _ = Services.HvciSafeProcess.StartWithTimeoutAsync(runProcessInfo, TimeSpan.FromSeconds(10), cancellationToken);
                    
                    _logger.LogWarning("🔄 Reinstall task triggered. Service will be restarted...");
                }
                else
                {
                    _logger.LogError("Failed to create scheduled task. Output: {Output}, Error: {Error}", output, error);
                    
                    // Fallback: try direct process launch
                    _logger.LogInformation("Attempting direct process launch as fallback...");
                    var fallbackProcessInfo = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File \"{tempScriptPath}\"",
                        UseShellExecute = true,
                        Verb = "runas",
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    
                    var fallbackProcess = await Services.HvciSafeProcess.StartWithTimeoutAsync(
                        fallbackProcessInfo, TimeSpan.FromSeconds(15), cancellationToken);
                    
                    if (fallbackProcess != null)
                    {
                        _logger.LogInformation("Fallback reinstall process started (PID: {PID})", fallbackProcess.Id);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger last resort reinstall");
        }
    }
}
