using System.Diagnostics;
using System.Management;
using System.Net.Http.Json;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Text.Json;

[assembly: SupportedOSPlatform("windows")]

/// <summary>
/// Standalone updater executable that runs outside the main service.
/// This allows updating all service files including the main executable.
/// Designed to run in SYSTEM context from Windows Service.
/// Reports progress to the server for UI visibility.
/// Supports multiple server URLs with automatic failover.
/// 
/// Usage: AgentUpdater.exe "source_path" "destination_path" "service_name"
/// </summary>

// Use a well-known path that works for SYSTEM account
const string LogDirectory = @"C:\ProgramData\ComputerInventory";
var logFile = Path.Combine(LogDirectory, "updater.log");

// Ensure log directory exists first thing
try
{
    if (!Directory.Exists(LogDirectory))
        Directory.CreateDirectory(LogDirectory);
}
catch { }

// Clean up old log file at start of each update to avoid confusion
// Only keep the current update attempt's logs
try
{
    if (File.Exists(logFile))
        File.Delete(logFile);
}
catch { }

void Log(string message)
{
    var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
    
    // Write to console (may not be visible in SYSTEM context)
    try { Console.WriteLine(logMessage); } catch { }
    
    // Always write to file
    try
    {
        File.AppendAllText(logFile, logMessage + Environment.NewLine);
    }
    catch { }
}

// Helper function to create HttpClient with URL failover support
async Task<HttpClient?> CreateHttpClientWithFailoverAsync(UpdaterConfig config, Action<string> log)
{
    var urls = new List<string>();
    
    // Primary URL
    if (!string.IsNullOrEmpty(config.ServerUrl))
    {
        urls.Add(config.ServerUrl.TrimEnd('/') + "/");
    }
    
    // Fallback URLs
    if (config.FallbackUrls != null)
    {
        foreach (var url in config.FallbackUrls)
        {
            if (!string.IsNullOrEmpty(url))
            {
                var normalized = url.TrimEnd('/') + "/";
                if (!urls.Contains(normalized))
                {
                    urls.Add(normalized);
                }
            }
        }
    }
    
    if (urls.Count == 0)
    {
        log("No server URLs configured");
        return null;
    }
    
    log($"Configured URLs: {string.Join(", ", urls)}");
    
    // Try each URL until we find one that works
    foreach (var url in urls)
    {
        HttpClient? client = null;
        try
        {
            client = new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(10) };
            if (!string.IsNullOrEmpty(config.ApiKey))
            {
                client.DefaultRequestHeaders.Add("X-API-Key", config.ApiKey);
            }
            if (!string.IsNullOrEmpty(config.AgentSessionId))
            {
                client.DefaultRequestHeaders.Add("X-Agent-Session-Id", config.AgentSessionId);
            }
            
            // Quick health check with timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var response = await client.GetAsync("api/health", cts.Token);
            
            if (response.IsSuccessStatusCode)
            {
                log($"Connected to server: {url}");
                return client;
            }
            
            log($"Server not responding: {url}");
            client.Dispose();
        }
        catch (Exception ex)
        {
            log($"Failed to connect to {url}: {ex.Message}");
            client?.Dispose();
        }
    }
    
    // If no URL worked, return client for primary URL anyway (will retry during progress reports)
    log("No server URLs responding, using primary URL for retry attempts");
    var fallbackClient = new HttpClient { BaseAddress = new Uri(urls[0]), Timeout = TimeSpan.FromSeconds(10) };
    if (!string.IsNullOrEmpty(config.ApiKey))
    {
        fallbackClient.DefaultRequestHeaders.Add("X-API-Key", config.ApiKey);
    }
    if (!string.IsNullOrEmpty(config.AgentSessionId))
    {
        fallbackClient.DefaultRequestHeaders.Add("X-Agent-Session-Id", config.AgentSessionId);
    }
    return fallbackClient;
}

// Start logging immediately
Log("========================================");
Log("=== Agent Updater Started ===");
Log($"Process ID: {Environment.ProcessId}");
Log($"User: {Environment.UserName}");
Log($"Working Directory: {Environment.CurrentDirectory}");
Log($"Command Line: {Environment.CommandLine}");
Log($"Args Count: {args.Length}");
for (int i = 0; i < args.Length; i++)
{
    Log($"  Arg[{i}]: {args[i]}");
}

// Validate arguments early
if (args.Length < 3)
{
    Log("ERROR: Insufficient arguments");
    Log("Usage: AgentUpdater.exe <source_path> <destination_path> <service_name>");
    Log("  source_path      - Path to extracted update files");
    Log("  destination_path - Path to service installation directory");
    Log("  service_name     - Name of the Windows service to restart");
    return 1;
}

var sourcePath = args[0];
var destinationPath = args[1];
var serviceName = args[2];

Log($"Source: {sourcePath}");
Log($"Destination: {destinationPath}");
Log($"Service: {serviceName}");

// Validate paths exist
if (!Directory.Exists(sourcePath))
{
    Log($"ERROR: Source path does not exist: {sourcePath}");
    return 1;
}

if (!Directory.Exists(destinationPath))
{
    Log($"ERROR: Destination path does not exist: {destinationPath}");
    return 1;
}

// ========================================
// SERVER PROTECTION CHECKS
// Prevent the updater from running on the server machine
// ========================================
Log("Performing server protection checks...");

// Check 1: Windows Server OS detection
if (IsWindowsServer(Log))
{
    Log("========================================");
    Log("⛔ UPDATE BLOCKED: Running on Windows Server OS");
    Log("The agent updater should not run on server machines.");
    Log("This machine appears to be a Windows Server.");
    Log("========================================");
    return 2; // Special exit code for server detection
}

// Check 2: API Server process running locally
if (IsApiServerRunningLocally(Log))
{
    Log("========================================");
    Log("⛔ UPDATE BLOCKED: API Server detected on this machine");
    Log("The agent updater should not run on the server hosting the API.");
    Log("ComputerInventory.Api process is running on this machine.");
    Log("========================================");
    return 2;
}

Log("Server protection checks passed - this is a client machine.");

// Configuration for progress reporting (optional)
HttpClient? httpClient = null;
string? serialNumber = null;
var maxBackupsToKeep = 1;

// Look for config in the source directory first (passed by UpdateService)
var configPath = Path.Combine(sourcePath, "updater-config.json");
Log($"Looking for config at: {configPath}");

if (!File.Exists(configPath))
{
    // Fall back to temp path (SYSTEM's temp is different)
    configPath = Path.Combine(Path.GetDirectoryName(sourcePath) ?? "", "updater-config.json");
    Log($"Config not in source, trying: {configPath}");
}

if (File.Exists(configPath))
{
    try
    {
        Log("Config file found, reading...");
        var configJson = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<UpdaterConfig>(configJson);
        if (config != null && !string.IsNullOrEmpty(config.ServerUrl))
        {
            serialNumber = config.SerialNumber;
            maxBackupsToKeep = config.MaxBackupsToKeep > 0 ? config.MaxBackupsToKeep : 1;
            httpClient = await CreateHttpClientWithFailoverAsync(config, Log);
            Log($"Config loaded: serial={serialNumber}, URLs configured");
            
            // Check if server requested a full reinstall instead of normal update
            if (config.ReinstallRequired && !string.IsNullOrEmpty(config.InstallScriptUrl))
            {
                Log("========================================");
                Log("🔧 REINSTALL REQUIRED - Server requested full reinstall");
                Log($"Install script URL: {config.InstallScriptUrl}");
                Log("========================================");
                
                // Report that we're doing a reinstall
                try
                {
                    var progressRequest = new
                    {
                        SerialNumber = serialNumber,
                        Status = 7, // InProgress
                        Progress = 50,
                        Message = "🔧 Executing full reinstall as requested by server..."
                    };
                    if (httpClient != null)
                    {
                        using var progressCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await httpClient.PostAsJsonAsync("api/agent/update-progress", progressRequest, progressCts.Token);
                    }
                }
                catch { }
                
                // Execute reinstall via PowerShell
                var reinstallResult = ExecuteReinstall(config.InstallScriptUrl, Log);
                
                // Report result
                try
                {
                    var finalRequest = new
                    {
                        SerialNumber = serialNumber,
                        Status = reinstallResult ? 10 : 11, // Success or Failed
                        Progress = 100,
                        Message = reinstallResult ? "✅ Reinstall initiated successfully" : "❌ Reinstall failed"
                    };
                    if (httpClient != null)
                    {
                        using var finalCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                        await httpClient.PostAsJsonAsync("api/agent/update-progress", finalRequest, finalCts.Token);
                    }
                }
                catch { }
                
                return reinstallResult ? 0 : 1;
            }
        }
    }
    catch (Exception ex)
    {
        Log($"Warning: Error loading config: {ex.Message}");
    }
}
else
{
    Log("Config file not found, progress reporting disabled");
}

async Task ReportProgressAsync(int status, int progress, string message)
{
    if (httpClient == null || string.IsNullOrEmpty(serialNumber)) return;

    try
    {
        var request = new
        {
            SerialNumber = serialNumber,
            Status = status,
            Progress = progress,
            Message = message
        };
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await httpClient.PostAsJsonAsync("api/agent/update-progress", request, cts.Token);
    }
    catch (Exception ex)
    {
        Log($"Warning: Could not report progress: {ex.Message}");
    }
}

try
{
    // ========================================
    // DETECT AGENT INSTALLATION TYPE
    // ========================================
    Log("Detecting current agent installation type...");
    await ReportProgressAsync(7, 78, "Detecting agent type...");
    
    var detectedType = DetectAgentType(serviceName, Log);
    Log($"Detected agent type: {detectedType}");
    
    // Handle based on detected type
    if (detectedType == "LegacyMKS")
    {
        Log("========================================");
        Log("🔄 LEGACY MKS INSTALLATION DETECTED");
        Log("Cleaning up legacy installation before update...");
        Log("========================================");
        await ReportProgressAsync(7, 80, "Cleaning up legacy MKS installation...");
        
        // Clean up the legacy MKS installation only
        CleanupLegacyInstallations(serviceName, Log);
        
        Log("Legacy MKS cleanup complete. Proceeding with update...");
    }
    else if (detectedType == "ScheduledTask")
    {
        Log("========================================");
        Log("✓ SCHEDULED TASK INSTALLATION DETECTED");
        Log("This is the preferred installation type.");
        Log("========================================");
    }
    else if (detectedType == "WindowsService")
    {
        Log("========================================");
        Log("⚠️ WINDOWS SERVICE INSTALLATION DETECTED");
        Log("Note: Scheduled Task is now preferred (avoids HVCI issues).");
        Log("========================================");
    }
    else
    {
        Log("No existing installation found - this will be a fresh install.");
    }

    // Wait a moment for any processes to release handles
    Log("Waiting for file handles to be released...");
    await ReportProgressAsync(7, 82, "Waiting for file handles...");
    Thread.Sleep(2000);

    // Stop the agent based on detected type
    Log($"Stopping agent (type: {detectedType})...");
    await ReportProgressAsync(7, 84, $"Stopping {detectedType}...");
    
    var agentStopped = false;
    if (detectedType == "ScheduledTask")
    {
        agentStopped = StopScheduledTask(serviceName, TimeSpan.FromSeconds(60), Log);
    }
    else if (detectedType == "WindowsService")
    {
        agentStopped = StopService(serviceName, TimeSpan.FromSeconds(60));
    }
    else
    {
        // No agent to stop, or unknown type - just continue
        agentStopped = true;
    }
    
    // ALWAYS force kill processes - even if stop succeeded
    // This handles cases where the process is still running after stop
    Log("Force killing any remaining agent processes...");
    await ReportProgressAsync(7, 86, "Terminating agent processes...");
    ForceKillAgentProcess(Log);
    
    if (!agentStopped)
    {
        Log("Warning: Agent could not be stopped gracefully.");
    }

    // Additional wait to ensure all handles are released
    Log("Waiting for file handles to release...");
    Thread.Sleep(3000);
    
    // Try to ensure files are not locked by checking if we can open them
    Log("Verifying file access...");
    var mainExePaths = new[] {
        Path.Combine(destinationPath, "MKS.InventoryAgent.exe"),
        Path.Combine(destinationPath, "ComputerInventory.ThinClient.exe")
    };
    
    foreach (var exePath in mainExePaths)
    {
        if (File.Exists(exePath))
        {
            var retries = 5;
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    // Try to open the file for writing to verify it's not locked
                    using var fs = new FileStream(exePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    Log($"File is accessible: {Path.GetFileName(exePath)}");
                    break;
                }
                catch (IOException) when (i < retries - 1)
                {
                    Log($"File still locked (attempt {i + 1}/{retries}): {Path.GetFileName(exePath)}, waiting...");
                    Thread.Sleep(2000);
                    // Try force kill again
                    ForceKillAgentProcess(Log);
                }
                catch (IOException)
                {
                    Log($"ERROR: File still locked after {retries} attempts: {Path.GetFileName(exePath)}");
                    // Continue anyway - we'll retry during file copy
                }
            }
        }
    }

    // Backup current installation (in case we need to rollback)
    var backupPath = destinationPath + "_backup_" + DateTime.Now.ToString("yyyyMMddHHmmss");
    Log($"Creating backup at {backupPath}...");
    await ReportProgressAsync(8, 88, "Creating backup of current installation...");
    try
    {
        CopyDirectory(destinationPath, backupPath, Log);
        Log("Backup created successfully.");
    }
    catch (Exception ex)
    {
        Log($"Warning: Could not create backup: {ex.Message}");
        // Continue anyway - update is more important
    }

    // Copy new files
    Log("Copying update files...");
    await ReportProgressAsync(9, 90, "Copying new files...");
    
    var sourceFiles = Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories);
    var totalFiles = sourceFiles.Length;
    var filesCopied = 0;
    var filesSkipped = 0;
    var errors = new List<string>();

    Log($"Found {totalFiles} files to process");

    foreach (var sourceFile in sourceFiles)
    {
        var relativePath = Path.GetRelativePath(sourcePath, sourceFile);
        var destFile = Path.Combine(destinationPath, relativePath);
        var destDir = Path.GetDirectoryName(destFile);
        var fileName = Path.GetFileName(sourceFile);

        // Skip the updater itself
        if (fileName.Equals("AgentUpdater.exe", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("AgentUpdater.pdb", StringComparison.OrdinalIgnoreCase))
        {
            Log($"Skipping updater file: {relativePath}");
            filesSkipped++;
            continue;
        }

        // Skip install/uninstall scripts — only needed for initial install, not updates.
        // Also avoids BitDefender blocking script file writes from the updater process.
        if (fileName.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            Log($"Skipping install script: {relativePath}");
            filesSkipped++;
            continue;
        }

        // Skip config files
        if (fileName.Equals("updater-config.json", StringComparison.OrdinalIgnoreCase))
        {
            Log($"Skipping config file: {relativePath}");
            filesSkipped++;
            continue;
        }

        // Preserve user configuration - merge critical settings from update package
        if (fileName.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase) && File.Exists(destFile))
        {
            Log($"Preserving existing config, merging critical settings: {relativePath}");
            try
            {
                var existingJson = System.Text.Json.JsonDocument.Parse(File.ReadAllText(destFile));
                var updateJson = System.Text.Json.JsonDocument.Parse(File.ReadAllText(sourceFile));

                // If the update package has a non-empty ApiKey and existing has empty, merge it
                var existingApiKey = "";
                var updateApiKey = "";
                if (existingJson.RootElement.TryGetProperty("ApiSettings", out var existApi) &&
                    existApi.TryGetProperty("ApiKey", out var existKey))
                    existingApiKey = existKey.GetString() ?? "";
                if (updateJson.RootElement.TryGetProperty("ApiSettings", out var updateApi) &&
                    updateApi.TryGetProperty("ApiKey", out var updateKey))
                    updateApiKey = updateKey.GetString() ?? "";

                if (string.IsNullOrEmpty(existingApiKey) && !string.IsNullOrEmpty(updateApiKey))
                {
                    Log($"Merging API key from update package into existing config");
                    var configText = File.ReadAllText(destFile);
                    // Replace empty ApiKey with the one from update package
                    configText = configText.Replace("\"ApiKey\": \"\"", $"\"ApiKey\": \"{updateApiKey}\"");
                    File.WriteAllText(destFile, configText);
                }
            }
            catch (Exception ex)
            {
                Log($"Warning: Failed to merge config settings: {ex.Message}");
            }
            filesSkipped++;
            continue;
        }

        // Ensure destination directory exists
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            Log($"Creating directory: {destDir}");
            Directory.CreateDirectory(destDir);
        }

        // Retry logic for files that might be locked
        var copied = false;
        var maxRetries = 10;
        for (int retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                File.Copy(sourceFile, destFile, true);
                filesCopied++;
                copied = true;
                Log($"Copied: {relativePath}");
                break;
            }
            catch (IOException ex) when (retry < maxRetries - 1)
            {
                Log($"File locked (attempt {retry + 1}/{maxRetries}): {relativePath} - {ex.Message}");
                Thread.Sleep(1000);
            }
            catch (UnauthorizedAccessException ex) when (retry < maxRetries - 1)
            {
                Log($"Access denied (attempt {retry + 1}/{maxRetries}): {relativePath} - {ex.Message}");
                // Try to remove read-only flag
                try
                {
                    var attrs = File.GetAttributes(destFile);
                    if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    {
                        File.SetAttributes(destFile, attrs & ~FileAttributes.ReadOnly);
                    }
                }
                catch { }
                Thread.Sleep(500);
            }
        }

        if (!copied)
        {
            var error = $"Failed to copy after {maxRetries} attempts: {relativePath}";
            Log($"ERROR: {error}");
            errors.Add(error);
        }

        // Report progress periodically
        if (filesCopied % 5 == 0)
        {
            var copyProgress = 90 + (filesCopied * 5 / Math.Max(totalFiles, 1));
            await ReportProgressAsync(9, Math.Min(copyProgress, 95), $"Copying files... {filesCopied}/{totalFiles}");
        }
    }

    Log($"Update complete: {filesCopied} files copied, {filesSkipped} skipped, {errors.Count} errors");

    if (errors.Count > 0)
    {
        Log("Errors encountered:");
        foreach (var error in errors)
        {
            Log($"  - {error}");
        }
    }

    // Unblock all files in the destination to remove Zone.Identifier
    // This prevents "downloaded from internet" blocking on systems with strict security
    Log("Unblocking updated files (removing Zone.Identifier)...");
    await ReportProgressAsync(9, 95, "Unblocking files...");
    UnblockFiles(destinationPath, Log);

    // Clean up the persisted config backup to prevent potential startup hangs
    // on CI-enforced systems. The appsettings.json is preserved so this backup
    // is no longer needed.
    var persistedConfigPath = Path.Combine(LogDirectory, "agent-config-backup.json");
    if (File.Exists(persistedConfigPath))
    {
        try
        {
            Log($"Cleaning up persisted config backup: {persistedConfigPath}");
            File.Delete(persistedConfigPath);
            Log("Persisted config backup deleted (prevents CI startup hangs)");
        }
        catch (Exception ex)
        {
            Log($"Warning: Could not delete persisted config backup: {ex.Message}");
        }
    }

    // Start the agent based on the detected type
    Log($"Starting agent (type: {detectedType})...");
    await ReportProgressAsync(10, 96, $"Starting {detectedType}...");
    
    var agentStarted = false;
    // Extended timeout (90 seconds) for systems with Code Integrity enforcement
    // which may take longer to verify unsigned executables
    
    if (detectedType == "ScheduledTask")
    {
        agentStarted = StartScheduledTask(serviceName, TimeSpan.FromSeconds(90), Log);
    }
    else if (detectedType == "WindowsService")
    {
        agentStarted = StartService(serviceName, TimeSpan.FromSeconds(90));
    }
    else
    {
        // For fresh installs or unknown types, try scheduled task first
        Log("Attempting to start as Scheduled Task...");
        agentStarted = StartScheduledTask(serviceName, TimeSpan.FromSeconds(90), Log);
        
        if (!agentStarted)
        {
            Log("Scheduled Task not found, trying Windows Service...");
            agentStarted = StartService(serviceName, TimeSpan.FromSeconds(90));
        }
    }
    
    if (agentStarted)
    {
        Log("Agent started successfully!");
        await ReportProgressAsync(11, 100, "Update completed successfully!");
    }
    else
    {
        Log("Warning: Agent may not have started correctly. Check Windows Event Log.");
        await ReportProgressAsync(99, 100, "Agent may not have started correctly");
    }

    // Clean up old backups — keep only the configured number
    CleanupOldBackups(destinationPath, maxBackupsToKeep, Log);

    // Clean up the update temp directory now that update succeeded
    CleanupUpdateTempDirectory(sourcePath, Log);

    Log("=== Update completed successfully ===");
    Log("========================================");
    httpClient?.Dispose();
    return 0;
}
catch (Exception ex)
{
    Log($"FATAL ERROR: {ex}");
    await ReportProgressAsync(99, 0, $"Update failed: {ex.Message}");

    // Try to restart the agent even if update failed
    try
    {
        Log("Attempting to restart agent after error...");
        
        // Try Scheduled Task first (preferred), then Windows Service
        var restarted = StartScheduledTask(serviceName, TimeSpan.FromSeconds(30), Log);
        if (!restarted)
        {
            Log("Scheduled Task restart failed, trying Windows Service...");
            StartService(serviceName, TimeSpan.FromSeconds(30));
        }
    }
    catch (Exception restartEx)
    {
        Log($"Failed to restart agent: {restartEx.Message}");
    }

    httpClient?.Dispose();
    return 1;
}

static bool StopService(string serviceName, TimeSpan timeout)
{
    var success = true;
    
    // Stop ALL known service names (both new and legacy)
    var serviceNames = new[] { serviceName, "ComputerInventoryAgent", "MKSInventoryAgent", "MKS.InventoryAgent" };
    var uniqueNames = serviceNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    
    foreach (var svcName in uniqueNames)
    {
        try
        {
            using var sc = new ServiceController(svcName);
            
            // Try to get status - this will throw if service doesn't exist
            var status = sc.Status;
            Console.WriteLine($"Found service '{svcName}' with status: {status}");
            
            if (status == ServiceControllerStatus.Stopped)
            {
                Console.WriteLine($"Service '{svcName}' is already stopped.");
                continue;
            }

            if (status != ServiceControllerStatus.StopPending)
            {
                Console.WriteLine($"Stopping service '{svcName}'...");
                sc.Stop();
            }

            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < timeout)
            {
                sc.Refresh();
                if (sc.Status == ServiceControllerStatus.Stopped)
                {
                    Console.WriteLine($"Service '{svcName}' stopped successfully.");
                    break;
                }
                Thread.Sleep(500);
            }
            
            if (sc.Status != ServiceControllerStatus.Stopped)
            {
                Console.WriteLine($"Warning: Service '{svcName}' did not stop within timeout.");
                success = false;
            }
        }
        catch (InvalidOperationException)
        {
            // Service doesn't exist - that's fine
            Console.WriteLine($"Service '{svcName}' not found (ok).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping service '{svcName}': {ex.Message}");
            success = false;
        }
    }
    
    return success;
}

/// <summary>
/// Execute a full reinstall by downloading and executing the install script.
/// This is used when the server detects that a normal update isn't working
/// (e.g., old client that doesn't understand ForceReinstall flag).
/// </summary>
static bool ExecuteReinstall(string installScriptUrl, Action<string> log)
{
    try
    {
        log("Executing reinstall via PowerShell...");
        
        // First, stop the service to avoid conflicts
        // First, try to stop the agent - try scheduled task, then service
        log("Stopping ComputerInventoryAgent...");
        
        // Try scheduled task first (preferred installation type)
        StopScheduledTask("ComputerInventoryAgent", TimeSpan.FromSeconds(30), log);
        
        // Also try Windows Service in case it exists
        try
        {
            using var sc = new ServiceController("ComputerInventoryAgent");
            if (sc.Status != ServiceControllerStatus.Stopped)
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            }
            log("Windows Service stopped.");
        }
        catch (InvalidOperationException)
        {
            // Service doesn't exist - that's fine
            log("No Windows Service found (OK).");
        }
        catch (Exception ex)
        {
            log($"Warning: Could not stop Windows Service: {ex.Message}");
        }
        
        // Always force kill processes as a fallback
        try
        {
            log("Force killing agent processes...");
            var processes = Process.GetProcessesByName("ComputerInventory.ThinClient");
            foreach (var proc in processes)
            {
                try
                {
                    proc.Kill();
                    proc.WaitForExit(5000);
                    log($"Killed process {proc.Id}");
                }
                catch { }
                finally { proc.Dispose(); }
            }
        }
        catch { }
        
        // Build the PowerShell command to download and execute the install script
        // We use -ExecutionPolicy Bypass because we're running from SYSTEM context
        var psCommand = $"irm '{installScriptUrl}' | iex";
        
        log($"Running PowerShell command: {psCommand}");
        
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-ExecutionPolicy Bypass -NoProfile -NonInteractive -Command \"{psCommand}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        
        using var process = new Process { StartInfo = startInfo };
        
        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                log($"PS: {e.Data}");
        };
        
        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                log($"PS ERROR: {e.Data}");
        };
        
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        // Wait up to 5 minutes for the reinstall to complete
        var completed = process.WaitForExit(300000);
        
        if (!completed)
        {
            log("WARNING: PowerShell command did not complete in 5 minutes, but it may still be running.");
            // Don't kill it - the install script might still be working
            return true; // Assume success since the script was started
        }
        
        var exitCode = process.ExitCode;
        log($"PowerShell exited with code: {exitCode}");
        
        // Exit code 0 means success
        if (exitCode == 0)
        {
            log("✅ Reinstall script completed successfully.");
            return true;
        }
        else
        {
            log($"❌ Reinstall script exited with error code: {exitCode}");
            return false;
        }
    }
    catch (Exception ex)
    {
        log($"❌ Failed to execute reinstall: {ex.Message}");
        log($"Stack trace: {ex.StackTrace}");
        return false;
    }
}

static bool StartService(string serviceName, TimeSpan timeout)
{
    try
    {
        using var sc = new ServiceController(serviceName);

        if (sc.Status == ServiceControllerStatus.Running)
            return true;

        if (sc.Status != ServiceControllerStatus.StartPending)
        {
            sc.Start();
        }

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            sc.Refresh();
            if (sc.Status == ServiceControllerStatus.Running)
                return true;
            Thread.Sleep(500);
        }

        return false;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error starting service: {ex.Message}");
        return false;
    }
}

/// <summary>
/// Stops a Windows Scheduled Task using schtasks.exe /End.
/// </summary>
static bool StopScheduledTask(string taskName, TimeSpan timeout, Action<string> log)
{
    var taskNames = new[] { taskName, "ComputerInventoryAgent", "MKSInventoryAgent" };
    var uniqueNames = taskNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    var success = true;
    
    foreach (var name in uniqueNames)
    {
        try
        {
            log($"Stopping scheduled task '{name}'...");
            
            // Use schtasks.exe /End to stop the running task instance
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/End /TN \"{name}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            using var process = Process.Start(psi);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit((int)timeout.TotalMilliseconds);
                
                // Exit code 0 = success, 1 = task not running or not found (both OK)
                if (process.ExitCode == 0)
                {
                    log($"Scheduled task '{name}' stop command succeeded.");
                }
                else if (!string.IsNullOrWhiteSpace(error) && !error.Contains("is not currently running"))
                {
                    log($"Warning: schtasks /End returned code {process.ExitCode}: {error.Trim()}");
                }
                else
                {
                    log($"Scheduled task '{name}' was not running (OK).");
                }
            }
        }
        catch (Exception ex)
        {
            log($"Error stopping scheduled task '{name}': {ex.Message}");
            success = false;
        }
    }
    
    // Give the task time to actually stop
    Thread.Sleep(1000);
    
    return success;
}

/// <summary>
/// Starts a Windows Scheduled Task using schtasks.exe /Run.
/// </summary>
static bool StartScheduledTask(string taskName, TimeSpan timeout, Action<string> log)
{
    try
    {
        log($"Starting scheduled task '{taskName}'...");
        
        // Use schtasks.exe /Run to start the task
        var psi = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = $"/Run /TN \"{taskName}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(psi);
        if (process != null)
        {
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit((int)timeout.TotalMilliseconds);
            
            if (process.ExitCode == 0)
            {
                log($"Scheduled task '{taskName}' started successfully.");
                
                // Verify it's actually running by checking the process
                Thread.Sleep(2000);
                var agentProcess = Process.GetProcessesByName("ComputerInventory.ThinClient");
                if (agentProcess.Length > 0)
                {
                    log($"Verified: Agent process is running (PID: {agentProcess[0].Id})");
                    foreach (var p in agentProcess) p.Dispose();
                    return true;
                }
                else
                {
                    log("Warning: Task started but process not found yet. It may be starting...");
                    return true; // Task was triggered, process may take a moment
                }
            }
            else
            {
                log($"Error: schtasks /Run returned code {process.ExitCode}: {error.Trim()}");
                return false;
            }
        }
        
        return false;
    }
    catch (Exception ex)
    {
        log($"Error starting scheduled task: {ex.Message}");
        return false;
    }
}

/// <summary>
/// Remove old backup directories, keeping only the N most recent.
/// Backups are named {installPath}_backup_yyyyMMddHHmmss.
/// </summary>
static void CleanupOldBackups(string installPath, int keepCount, Action<string> log)
{
    try
    {
        var parentDir = Path.GetDirectoryName(installPath);
        if (string.IsNullOrEmpty(parentDir) || !Directory.Exists(parentDir))
            return;

        var dirName = Path.GetFileName(installPath);
        var backupPrefix = dirName + "_backup_";

        var backupDirs = Directory.GetDirectories(parentDir, backupPrefix + "*")
            .OrderByDescending(d => d) // yyyyMMddHHmmss sorts chronologically
            .ToList();

        if (backupDirs.Count <= keepCount)
        {
            log($"Found {backupDirs.Count} backup(s), keeping all (limit: {keepCount}).");
            return;
        }

        var toDelete = backupDirs.Skip(keepCount).ToList();
        log($"Found {backupDirs.Count} backup(s), removing {toDelete.Count} old backup(s) (keeping {keepCount})...");

        foreach (var dir in toDelete)
        {
            try
            {
                Directory.Delete(dir, true);
                log($"Deleted old backup: {Path.GetFileName(dir)}");
            }
            catch (Exception ex)
            {
                log($"Warning: Could not delete backup {Path.GetFileName(dir)}: {ex.Message}");
            }
        }
    }
    catch (Exception ex)
    {
        log($"Warning: Backup cleanup failed: {ex.Message}");
    }
}

/// <summary>
/// Clean up the update temp directory after a successful update.
/// Removes downloaded zip, extracted files, and updater config.
/// </summary>
static void CleanupUpdateTempDirectory(string sourcePath, Action<string> log)
{
    try
    {
        // sourcePath is the extracted/ subfolder; go up to the temp root
        var tempRoot = Path.GetDirectoryName(sourcePath);
        if (string.IsNullOrEmpty(tempRoot) || !Directory.Exists(tempRoot))
            return;

        log($"Cleaning up update temp directory: {tempRoot}");

        // Delete downloaded zip (no longer needed for resume)
        var zipPath = Path.Combine(tempRoot, "update.zip");
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
            log("Deleted update.zip");
        }

        // Delete extracted directory
        var extractedPath = Path.Combine(tempRoot, "extracted");
        if (Directory.Exists(extractedPath))
        {
            Directory.Delete(extractedPath, true);
            log("Deleted extracted/ directory");
        }

        // Delete updater config (contains API key)
        var configPath = Path.Combine(tempRoot, "updater-config.json");
        if (File.Exists(configPath))
        {
            File.Delete(configPath);
            log("Deleted updater-config.json");
        }
    }
    catch (Exception ex)
    {
        log($"Warning: Temp directory cleanup failed: {ex.Message}");
    }
}

static void CopyDirectory(string sourceDir, string destDir, Action<string> log)
{
    Directory.CreateDirectory(destDir);

    foreach (var file in Directory.GetFiles(sourceDir))
    {
        var destFile = Path.Combine(destDir, Path.GetFileName(file));
        try
        {
            File.Copy(file, destFile, true);
        }
        catch (Exception ex)
        {
            log($"Warning: Could not backup {Path.GetFileName(file)}: {ex.Message}");
        }
    }

    foreach (var dir in Directory.GetDirectories(sourceDir))
    {
        // Skip logs directory in backup
        if (Path.GetFileName(dir).Equals("logs", StringComparison.OrdinalIgnoreCase))
            continue;
            
        var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
        CopyDirectory(dir, destSubDir, log);
    }
}

/// <summary>
/// Unblock files by removing Zone.Identifier alternate data stream.
/// This is equivalent to PowerShell's Unblock-File cmdlet.
/// Prevents Windows from blocking "downloaded from internet" files.
/// Required for systems with Code Integrity (HVCI/WDAC) enforcement.
/// </summary>
static void UnblockFiles(string directoryPath, Action<string> log)
{
    try
    {
        var unblockedCount = 0;
        var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
        
        foreach (var file in files)
        {
            try
            {
                // Remove the Zone.Identifier alternate data stream
                var zoneFile = file + ":Zone.Identifier";
                
                // Use native Windows API via P/Invoke to delete the stream
                // DeleteFile works on alternate data streams
                if (NativeDeleteFile(zoneFile))
                {
                    unblockedCount++;
                }
            }
            catch
            {
                // Silently ignore files that can't be unblocked
            }
        }
        
        log($"Unblocked {unblockedCount} files");
    }
    catch (Exception ex)
    {
        log($"Warning: Error during unblock: {ex.Message}");
    }
}

// P/Invoke for DeleteFile (can delete alternate data streams)
[System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
static extern bool DeleteFile(string lpFileName);

static bool NativeDeleteFile(string path)
{
    try
    {
        return DeleteFile(path);
    }
    catch
    {
        return false;
    }
}

// ========================================
// SERVER PROTECTION FUNCTIONS
// ========================================

/// <summary>
/// Detects if the current machine is running Windows Server OS.
/// Uses WMI to check the ProductType:
/// 1 = Workstation, 2 = Domain Controller, 3 = Server
/// </summary>
static bool IsWindowsServer(Action<string> log)
{
    try
    {
        using var searcher = new ManagementObjectSearcher("SELECT ProductType, Caption FROM Win32_OperatingSystem");
        foreach (ManagementObject obj in searcher.Get())
        {
            var productType = Convert.ToInt32(obj["ProductType"]);
            var caption = obj["Caption"]?.ToString() ?? "";
            
            log($"OS Detection: ProductType={productType}, Caption={caption}");
            
            // ProductType: 1=Workstation, 2=Domain Controller, 3=Server
            if (productType == 2 || productType == 3)
            {
                log($"⚠️ Windows Server detected: {caption}");
                return true;
            }
        }
        log("OS is a workstation/desktop edition.");
        return false;
    }
    catch (Exception ex)
    {
        log($"Warning: Could not detect OS type via WMI: {ex.Message}");
        // If we can't detect, assume it's safe to continue (workstation)
        return false;
    }
}

/// <summary>
/// Checks if the ComputerInventory API server is running on this machine.
/// This prevents the updater from accidentally updating the server.
/// </summary>
static bool IsApiServerRunningLocally(Action<string> log)
{
    try
    {
        // Check for API process
        var apiProcessNames = new[] { "ComputerInventory.Api", "dotnet" };
        
        foreach (var procName in apiProcessNames)
        {
            var processes = Process.GetProcessesByName(procName);
            foreach (var proc in processes)
            {
                try
                {
                    // For dotnet processes, check if it's running the API
                    if (procName == "dotnet")
                    {
                        var cmdLine = GetProcessCommandLine(proc.Id);
                        if (cmdLine != null && cmdLine.Contains("ComputerInventory.Api", StringComparison.OrdinalIgnoreCase))
                        {
                            log($"⚠️ API Server detected running as dotnet process (PID: {proc.Id})");
                            return true;
                        }
                    }
                    else
                    {
                        log($"⚠️ API Server process detected: {procName} (PID: {proc.Id})");
                        return true;
                    }
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        
        // Also check if the API is listening on common ports
        if (IsPortInUse(5010) || IsPortInUse(5000) || IsPortInUse(5001))
        {
            log("⚠️ Common API ports (5000, 5001, 5010) are in use - possible server machine");
            // Don't block on port check alone, just warn
        }
        
        log("No API server process detected on this machine.");
        return false;
    }
    catch (Exception ex)
    {
        log($"Warning: Could not check for API server process: {ex.Message}");
        return false;
    }
}

/// <summary>
/// Gets the command line of a process by PID using WMI.
/// </summary>
static string? GetProcessCommandLine(int processId)
{
    try
    {
        using var searcher = new ManagementObjectSearcher(
            $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}");
        foreach (ManagementObject obj in searcher.Get())
        {
            return obj["CommandLine"]?.ToString();
        }
    }
    catch { }
    return null;
}

/// <summary>
/// Checks if a TCP port is currently in use (listening).
/// </summary>
static bool IsPortInUse(int port)
{
    try
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "netstat",
            Arguments = $"-an",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(startInfo);
        if (process == null) return false;
        
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);
        
        return output.Contains($":{port}") && output.Contains("LISTENING");
    }
    catch
    {
        return false;
    }
}

// ========================================
// AGENT TYPE DETECTION AND CONTROL
// ========================================

/// <summary>
/// Detects the current agent installation type.
/// Returns: "ScheduledTask" (preferred), "WindowsService" (legacy), "LegacyMKS", or "None"
/// NOTE: ScheduledTask is now the PREFERRED installation type to avoid HVCI/SCM timeout issues.
/// </summary>
static string DetectAgentType(string serviceName, Action<string> log)
{
    // Check for legacy MKS.InventoryAgent process or installation
    try
    {
        var mksProcesses = Process.GetProcessesByName("MKS.InventoryAgent");
        if (mksProcesses.Length > 0)
        {
            foreach (var p in mksProcesses) p.Dispose();
            log("⚠️ Found running MKS.InventoryAgent process - LEGACY installation detected");
            return "LegacyMKS";
        }
        
        // Check for MKS installation directory
        var mksInstallPaths = new[]
        {
            @"C:\Program Files\MKS\InventoryAgent",
            @"C:\Program Files (x86)\MKS\InventoryAgent",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MKS", "InventoryAgent")
        };
        
        foreach (var path in mksInstallPaths)
        {
            if (Directory.Exists(path))
            {
                log($"⚠️ Found MKS installation directory: {path} - LEGACY installation detected");
                return "LegacyMKS";
            }
        }
        
        // Check for MKS scheduled task
        var mksTaskInfo = GetScheduledTaskInfo("MKSInventoryAgent");
        if (mksTaskInfo != null)
        {
            log($"⚠️ Found MKS Scheduled Task with state: {mksTaskInfo} - LEGACY installation detected");
            return "LegacyMKS";
        }
    }
    catch (Exception ex)
    {
        log($"Error checking for MKS legacy installation: {ex.Message}");
    }
    
    // Check for Scheduled Task (PREFERRED installation type - avoids HVCI/SCM timeout issues)
    try
    {
        var taskInfo = GetScheduledTaskInfo(serviceName);
        if (taskInfo != null)
        {
            log($"✓ Found Scheduled Task '{serviceName}' with state: {taskInfo} - this is the PREFERRED installation type");
            return "ScheduledTask";
        }
    }
    catch (Exception ex)
    {
        log($"Error checking for scheduled task: {ex.Message}");
    }
    
    // Check for Windows Service (legacy - can have HVCI/SCM timeout issues)
    try
    {
        using var sc = new ServiceController(serviceName);
        var status = sc.Status; // This will throw if service doesn't exist
        log($"⚠️ Found Windows Service '{serviceName}' with status: {status} - consider migrating to Scheduled Task");
        return "WindowsService";
    }
    catch (InvalidOperationException)
    {
        log($"No Windows Service named '{serviceName}' found.");
    }
    catch (Exception ex)
    {
        log($"Error checking for service: {ex.Message}");
    }
    
    // Nothing found
    log("No existing agent installation detected.");
    return "None";
}

/// <summary>
/// Cleans up legacy installations (Scheduled Tasks, MKS.InventoryAgent).
/// This migrates old installations to the new Windows Service architecture.
/// </summary>
static void CleanupLegacyInstallations(string serviceName, Action<string> log)
{
    log("========================================");
    log("Starting legacy MKS installation cleanup...");
    log("========================================");
    
    // 1. Kill any MKS legacy processes ONLY
    // Note: Do NOT kill ComputerInventory.ThinClient - that's our current agent!
    log("Step 1: Terminating MKS legacy processes...");
    var legacyProcessNames = new[] { "MKS.InventoryAgent" };
    foreach (var procName in legacyProcessNames)
    {
        try
        {
            var processes = Process.GetProcessesByName(procName);
            foreach (var proc in processes)
            {
                try
                {
                    log($"  Killing {procName} (PID: {proc.Id})...");
                    proc.Kill();
                    proc.WaitForExit(5000);
                    log($"  ✓ Process {proc.Id} terminated.");
                }
                catch (Exception ex)
                {
                    log($"  ⚠️ Could not kill process {proc.Id}: {ex.Message}");
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            log($"  Error processing {procName}: {ex.Message}");
        }
    }
    
    // 2. Remove MKS legacy Scheduled Tasks ONLY
    // Note: Do NOT remove ComputerInventoryAgent - that's our current installation!
    log("Step 2: Removing MKS legacy Scheduled Tasks...");
    var mksTaskNames = new[] { "MKSInventoryAgent", "MKS.InventoryAgent" };
    foreach (var taskName in mksTaskNames)
    {
        RemoveScheduledTask(taskName, log);
    }
    
    // 3. Remove MKS installation directories
    log("Step 3: Removing legacy MKS installation directories...");
    var mksInstallPaths = new[]
    {
        @"C:\Program Files\MKS\InventoryAgent",
        @"C:\Program Files (x86)\MKS\InventoryAgent",
        @"C:\Program Files\MKS",
        @"C:\Program Files (x86)\MKS"
    };
    
    foreach (var path in mksInstallPaths)
    {
        if (Directory.Exists(path))
        {
            try
            {
                log($"  Removing directory: {path}");
                Directory.Delete(path, true);
                log($"  ✓ Removed: {path}");
            }
            catch (Exception ex)
            {
                log($"  ⚠️ Could not remove {path}: {ex.Message}");
            }
        }
    }
    
    // 4. Clean up old registry entries (if any)
    log("Step 4: Cleaning up MKS registry entries...");
    CleanupLegacyRegistry(log);
    
    // 5. Remove old MKS log files
    log("Step 5: Archiving old MKS log files...");
    var oldLogPaths = new[]
    {
        @"C:\ProgramData\MKS\InventoryAgent",
        @"C:\ProgramData\MKSInventoryAgent"
    };
    
    foreach (var logPath in oldLogPaths)
    {
        if (Directory.Exists(logPath))
        {
            try
            {
                // Archive instead of delete - move to backup location
                var archivePath = logPath + "_archived_" + DateTime.Now.ToString("yyyyMMddHHmmss");
                log($"  Archiving {logPath} to {archivePath}");
                Directory.Move(logPath, archivePath);
                log($"  ✓ Archived old logs");
            }
            catch (Exception ex)
            {
                log($"  ⚠️ Could not archive {logPath}: {ex.Message}");
            }
        }
    }
    
    log("========================================");
    log("Legacy MKS cleanup completed.");
    log("========================================");
}

/// <summary>
/// Removes a scheduled task by name.
/// </summary>
static void RemoveScheduledTask(string taskName, Action<string> log)
{
    try
    {
        // First check if task exists
        var queryInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = $"/Query /TN \"{taskName}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        using (var queryProcess = Process.Start(queryInfo))
        {
            queryProcess?.WaitForExit(5000);
            if (queryProcess?.ExitCode != 0)
            {
                // Task doesn't exist
                return;
            }
        }
        
        log($"  Found Scheduled Task '{taskName}' - removing...");
        
        // Stop the task first
        var stopInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = $"/End /TN \"{taskName}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        using (var stopProcess = Process.Start(stopInfo))
        {
            stopProcess?.WaitForExit(10000);
        }
        
        Thread.Sleep(1000);
        
        // Delete the task
        var deleteInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = $"/Delete /TN \"{taskName}\" /F",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        using var deleteProcess = Process.Start(deleteInfo);
        var output = deleteProcess?.StandardOutput.ReadToEnd();
        var error = deleteProcess?.StandardError.ReadToEnd();
        deleteProcess?.WaitForExit(10000);
        
        if (deleteProcess?.ExitCode == 0)
        {
            log($"  ✓ Removed Scheduled Task '{taskName}'");
        }
        else
        {
            log($"  ⚠️ Could not remove task '{taskName}': {error}");
        }
    }
    catch (Exception ex)
    {
        log($"  Error removing task '{taskName}': {ex.Message}");
    }
}

/// <summary>
/// Cleans up legacy registry entries.
/// </summary>
static void CleanupLegacyRegistry(Action<string> log)
{
    try
    {
        // Remove MKS uninstall registry keys
        var uninstallKeys = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MKSInventoryAgent",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\MKS.InventoryAgent",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\MKSInventoryAgent",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\MKS.InventoryAgent"
        };
        
        foreach (var keyPath in uninstallKeys)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
                if (key != null)
                {
                    log($"  Found registry key: {keyPath}");
                    Microsoft.Win32.Registry.LocalMachine.DeleteSubKeyTree(keyPath, false);
                    log($"  ✓ Removed registry key");
                }
            }
            catch (Exception ex)
            {
                log($"  ⚠️ Could not remove registry key {keyPath}: {ex.Message}");
            }
        }
        
        // Remove MKS software keys
        var softwareKeys = new[]
        {
            @"SOFTWARE\MKS",
            @"SOFTWARE\WOW6432Node\MKS"
        };
        
        foreach (var keyPath in softwareKeys)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
                if (key != null)
                {
                    log($"  Found registry key: {keyPath}");
                    Microsoft.Win32.Registry.LocalMachine.DeleteSubKeyTree(keyPath, false);
                    log($"  ✓ Removed registry key");
                }
            }
            catch (Exception ex)
            {
                log($"  ⚠️ Could not remove registry key {keyPath}: {ex.Message}");
            }
        }
    }
    catch (Exception ex)
    {
        log($"  Error during registry cleanup: {ex.Message}");
    }
}

/// <summary>
/// Gets info about a scheduled task using schtasks.exe
/// </summary>
static string? GetScheduledTaskInfo(string taskName)
{
    try
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = $"/Query /TN \"{taskName}\" /FO CSV /NH",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        
        using var process = Process.Start(startInfo);
        if (process == null) return null;
        
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit(10000);
        
        if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
        {
            // Parse CSV output to get status
            var parts = output.Split(',');
            if (parts.Length >= 3)
            {
                return parts[2].Trim('"'); // Status field
            }
            return "Found";
        }
        
        return null;
    }
    catch
    {
        return null;
    }
}

/// <summary>
/// Force kills any running agent processes (handles both old and new naming).
/// Also kills by path if processes are running from the installation directory.
/// </summary>
static void ForceKillAgentProcess(Action<string> log)
{
    // Process names to kill (without .exe extension)
    var processNames = new[] { 
        "MKS.InventoryAgent", 
        "ComputerInventory.ThinClient",
        "MKSInventoryAgent",
        "InventoryAgent"
    };
    
    // First, try to kill by process name
    foreach (var procName in processNames)
    {
        try
        {
            var processes = Process.GetProcessesByName(procName);
            foreach (var proc in processes)
            {
                try
                {
                    log($"Killing process {procName} (PID: {proc.Id})...");
                    proc.Kill();
                    proc.WaitForExit(5000);
                    log($"Process {proc.Id} killed.");
                }
                catch (Exception ex)
                {
                    log($"Warning: Could not kill process {proc.Id}: {ex.Message}");
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            log($"Warning: Error killing {procName} processes: {ex.Message}");
        }
    }
    
    // Second pass: Kill ANY process running from the ComputerInventory installation directory
    // This handles cases where the executable has an unexpected name
    try
    {
        var installPaths = new[] {
            @"C:\Program Files\ComputerInventory",
            @"C:\Program Files (x86)\ComputerInventory",
            @"C:\Program Files\MKS\InventoryAgent",
            @"C:\Program Files (x86)\MKS\InventoryAgent"
        };
        
        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                string? exePath = null;
                try
                {
                    exePath = proc.MainModule?.FileName;
                }
                catch { }
                
                if (!string.IsNullOrEmpty(exePath))
                {
                    foreach (var installPath in installPaths)
                    {
                        if (exePath.StartsWith(installPath, StringComparison.OrdinalIgnoreCase))
                        {
                            log($"Found process running from install dir: {proc.ProcessName} (PID: {proc.Id}) at {exePath}");
                            try
                            {
                                proc.Kill();
                                proc.WaitForExit(5000);
                                log($"Process {proc.Id} killed (by path match).");
                            }
                            catch (Exception ex)
                            {
                                log($"Warning: Could not kill process {proc.Id}: {ex.Message}");
                            }
                            break;
                        }
                    }
                }
            }
            catch { }
            finally
            {
                proc.Dispose();
            }
        }
    }
    catch (Exception ex)
    {
        log($"Warning: Error scanning processes by path: {ex.Message}");
    }
    
    // Give processes time to fully terminate and release file handles
    Thread.Sleep(2000);
    
    // Third pass: Try taskkill as a last resort (more aggressive)
    log("Running taskkill as backup...");
    try
    {
        foreach (var procName in new[] { "MKS.InventoryAgent", "ComputerInventory.ThinClient" })
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/F /IM {procName}.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            
            using var killProc = Process.Start(startInfo);
            killProc?.WaitForExit(5000);
        }
    }
    catch (Exception ex)
    {
        log($"Warning: taskkill backup failed: {ex.Message}");
    }
    
    // Final wait for handles to be released
    Thread.Sleep(1000);
}

/// <summary>
/// Configuration for the updater to connect back to the server.
/// Supports multiple URLs with automatic failover.
/// </summary>
class UpdaterConfig
{
    public string? ServerUrl { get; set; }
    public string[]? FallbackUrls { get; set; }
    public string? ApiKey { get; set; }
    public string? SerialNumber { get; set; }
    /// <summary>
    /// If true, skip normal update and perform a full reinstall by downloading and executing the install script.
    /// This is used to fix old clients that don't have newer update handling code.
    /// </summary>
    public bool ReinstallRequired { get; set; }
    /// <summary>
    /// URL to download the install script from (e.g., http://server/api/agent/download/install-script)
    /// </summary>
    public string? InstallScriptUrl { get; set; }
    /// <summary>
    /// Number of old installation backups to keep after a successful update. Default: 1.
    /// </summary>
    public int MaxBackupsToKeep { get; set; } = 1;
    /// <summary>
    /// Agent session ID for correlating requests across registration, heartbeat, and progress calls.
    /// </summary>
    public string? AgentSessionId { get; set; }
}
