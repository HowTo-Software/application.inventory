using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using ComputerInventory.Contracts;

namespace ComputerInventory.ThinClient.Services;

/// <summary>
/// Handles checking for updates and applying them via the external updater process.
/// The updater runs outside the service to allow file replacement while service is stopped.
/// Reports progress to server at each step for visibility in the UI.
/// </summary>
public class UpdateService : IUpdateService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UpdateService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IConfigPersistenceService _configPersistence;
    private readonly IServerConnectionManager _connectionManager;
    private readonly string _apiKey;
    private readonly string _installPath;
    private readonly string _updateTempPath;
    private string? _serialNumber;

    public UpdateService(
        IHttpClientFactory httpClientFactory,
        ILogger<UpdateService> logger,
        IConfiguration configuration,
        IConfigPersistenceService configPersistence,
        IServerConnectionManager connectionManager)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _configuration = configuration;
        _configPersistence = configPersistence;
        _connectionManager = connectionManager;
        _apiKey = _configuration["ApiSettings:ApiKey"] ?? string.Empty;
        _installPath = AppContext.BaseDirectory;
        // Use ProgramData instead of system temp — SYSTEM's temp (C:\windows\SystemTemp)
        // blocks creation of script files (.cmd/.ps1) due to security policies
        _updateTempPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "ComputerInventory", "Update");
    }

    public string GetCurrentVersion()
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
        client.DefaultRequestHeaders.Add("X-Agent-Session-Id", _connectionManager.AgentSessionId);
        return client;
    }

    /// <summary>
    /// Report update progress to the server
    /// </summary>
    private async Task ReportProgressAsync(AgentUpdateStatus status, int progress, string message, string? errorLogs = null)
    {
        try
        {
            if (string.IsNullOrEmpty(_serialNumber))
            {
                _serialNumber = await GetSerialNumberAsync();
            }

            var client = CreateClient();
            
            // If this is a failure, collect error logs to send
            string? logsToSend = errorLogs;
            if (status == AgentUpdateStatus.Failed && string.IsNullOrEmpty(logsToSend))
            {
                logsToSend = await CollectErrorLogsAsync();
            }
            
            var request = new
            {
                SerialNumber = _serialNumber,
                Status = (int)status,
                Progress = progress,
                Message = message,
                ErrorLogs = logsToSend
            };

            var response = await client.PostAsJsonAsync("api/agent/update-progress", request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to report update progress: {Status}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Don't fail the update if progress reporting fails
            _logger.LogWarning(ex, "Error reporting update progress");
        }
    }
    
    /// <summary>
    /// Collect recent error logs to send to server for diagnostics
    /// </summary>
    private async Task<string?> CollectErrorLogsAsync()
    {
        try
        {
            var logs = new System.Text.StringBuilder();
            logs.AppendLine($"=== Update Error Logs Collected at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            logs.AppendLine($"Agent Version: {GetType().Assembly.GetName().Version}");
            logs.AppendLine($"Machine: {Environment.MachineName}");
            logs.AppendLine($"OS: {Environment.OSVersion}");
            logs.AppendLine();
            
            // Collect service-startup.log
            var startupLogPath = @"C:\ProgramData\ComputerInventory\service-startup.log";
            if (File.Exists(startupLogPath))
            {
                logs.AppendLine("=== service-startup.log (last 100 lines) ===");
                try
                {
                    var lines = await File.ReadAllLinesAsync(startupLogPath);
                    var recentLines = lines.Skip(Math.Max(0, lines.Length - 100));
                    foreach (var line in recentLines)
                    {
                        logs.AppendLine(line);
                    }
                }
                catch (Exception ex)
                {
                    logs.AppendLine($"Error reading startup log: {ex.Message}");
                }
                logs.AppendLine();
            }
            
            // Collect agent log files (most recent)
            var exePath = AppContext.BaseDirectory;
            var logsPath = Path.Combine(exePath, "logs");
            if (Directory.Exists(logsPath))
            {
                var logFiles = Directory.GetFiles(logsPath, "agent-*.log")
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .Take(2);  // Get the 2 most recent log files
                    
                foreach (var logFile in logFiles)
                {
                    logs.AppendLine($"=== {Path.GetFileName(logFile)} (last 200 lines) ===");
                    try
                    {
                        var lines = await File.ReadAllLinesAsync(logFile);
                        var recentLines = lines.Skip(Math.Max(0, lines.Length - 200));
                        foreach (var line in recentLines)
                        {
                            logs.AppendLine(line);
                        }
                    }
                    catch (Exception ex)
                    {
                        logs.AppendLine($"Error reading log file: {ex.Message}");
                    }
                    logs.AppendLine();
                }
            }
            
            // Collect updater log if exists
            var updaterLogPath = Path.Combine(_updateTempPath, "updater.log");
            if (File.Exists(updaterLogPath))
            {
                logs.AppendLine("=== updater.log ===");
                try
                {
                    var content = await File.ReadAllTextAsync(updaterLogPath);
                    logs.AppendLine(content);
                }
                catch (Exception ex)
                {
                    logs.AppendLine($"Error reading updater log: {ex.Message}");
                }
                logs.AppendLine();
            }
            
            // Limit total size to avoid huge payloads (max ~100KB)
            var result = logs.ToString();
            if (result.Length > 100000)
            {
                result = result.Substring(result.Length - 100000);
                result = "... (truncated) ...\n" + result;
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect error logs");
            return $"Failed to collect error logs: {ex.Message}";
        }
    }

    private Task<string> GetSerialNumberAsync()
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS");
            foreach (var obj in searcher.Get())
            {
                return Task.FromResult(obj["SerialNumber"]?.ToString()?.Trim() ?? "UNKNOWN");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get serial number for progress reporting");
        }
        return Task.FromResult("UNKNOWN");
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await ReportProgressAsync(AgentUpdateStatus.CheckingForUpdate, 0, "Checking for available updates...");
            
            var client = CreateClient();
            var currentVersion = GetCurrentVersion();
            
            // Ensure we have serial number for reinstall check
            if (string.IsNullOrEmpty(_serialNumber))
            {
                _serialNumber = await GetSerialNumberAsync();
            }
            
            // Include serialNumber so server can check if this computer needs a reinstall
            var response = await client.GetAsync(
                $"api/agent/check-update?currentVersion={Uri.EscapeDataString(currentVersion)}&serialNumber={Uri.EscapeDataString(_serialNumber)}", 
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("No update available or server returned {Status}", response.StatusCode);
                await ReportProgressAsync(AgentUpdateStatus.None, 0, "No updates available");
                return null;
            }

            var updateInfo = await response.Content.ReadFromJsonAsync<UpdateInfo>(cancellationToken: cancellationToken);
            
            if (updateInfo != null && !string.IsNullOrEmpty(updateInfo.Version))
            {
                var currentVer = new Version(currentVersion);
                var newVer = new Version(updateInfo.Version);
                
                if (newVer > currentVer)
                {
                    _logger.LogInformation("Update available: {Current} -> {New}", currentVersion, updateInfo.Version);
                    await ReportProgressAsync(AgentUpdateStatus.Pending, 5, $"Update available: {currentVersion} → {updateInfo.Version}");
                    return updateInfo;
                }
            }

            await ReportProgressAsync(AgentUpdateStatus.None, 0, "Already on latest version");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check for updates");
            await ReportProgressAsync(AgentUpdateStatus.Failed, 0, $"Failed to check for updates: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> DownloadAndApplyUpdateAsync(UpdateInfo updateInfo, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting update to version {Version}...", updateInfo.Version);
            
            // Save current configuration before update so it survives the upgrade
            await ReportProgressAsync(AgentUpdateStatus.Downloading, 5, "Saving configuration...");
            await _configPersistence.SaveConfigurationAsync();
            
            // Clean up previous update artifacts but preserve partial download for resume
            if (Directory.Exists(_updateTempPath))
            {
                var partialDownload = Path.Combine(_updateTempPath, "update.zip");
                var hadPartial = File.Exists(partialDownload);
                long partialSize = hadPartial ? new FileInfo(partialDownload).Length : 0;

                // Remove extracted files and other artifacts, but keep the zip for resume
                foreach (var dir in Directory.GetDirectories(_updateTempPath))
                {
                    Directory.Delete(dir, true);
                }
                foreach (var file in Directory.GetFiles(_updateTempPath).Where(f => !f.EndsWith("update.zip", StringComparison.OrdinalIgnoreCase)))
                {
                    File.Delete(file);
                }

                if (hadPartial)
                {
                    _logger.LogInformation("Found partial download ({Size:F1}MB) — will attempt resume", partialSize / (1024.0 * 1024.0));
                }
            }
            Directory.CreateDirectory(_updateTempPath);

            // Download the update package
            var packagePath = Path.Combine(_updateTempPath, "update.zip");
            if (!await DownloadUpdatePackageAsync(updateInfo, packagePath, cancellationToken))
            {
                await ReportProgressAsync(AgentUpdateStatus.Failed, 0, "Failed to download update package");
                return false;
            }

            // Verify checksum
            await ReportProgressAsync(AgentUpdateStatus.Verifying, 50, "Verifying package checksum...");
            if (!string.IsNullOrEmpty(updateInfo.Checksum))
            {
                if (!await VerifyChecksumAsync(packagePath, updateInfo.Checksum))
                {
                    _logger.LogError("Update package checksum verification failed!");
                    await ReportProgressAsync(AgentUpdateStatus.Failed, 0, "Checksum verification failed");
                    return false;
                }
            }

            // Extract to temp location — delete any leftover extracted dir first
            // to avoid "access denied" on read-only/locked files from a previous failed attempt
            await ReportProgressAsync(AgentUpdateStatus.Extracting, 60, "Extracting update files...");
            var extractPath = Path.Combine(_updateTempPath, "extracted");
            if (Directory.Exists(extractPath))
            {
                try { Directory.Delete(extractPath, true); }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not fully clean previous extracted dir, continuing..."); }
            }
            ZipFile.ExtractToDirectory(packagePath, extractPath, true);
            _logger.LogInformation("Update package extracted to {Path}", extractPath);

            // Unblock all extracted files (remove Zone.Identifier)
            // This is critical for systems with Code Integrity (HVCI/WDAC) enforcement
            await ReportProgressAsync(AgentUpdateStatus.Extracting, 65, "Unblocking extracted files...");
            UnblockFilesInDirectory(extractPath);
            _logger.LogInformation("Extracted files unblocked");

            // Copy the updater executable to temp (so it can run independently)
            await ReportProgressAsync(AgentUpdateStatus.PreparingUpdater, 70, "Preparing updater...");
            var updaterSource = Path.Combine(_installPath, "AgentUpdater.exe");
            var updaterDest = Path.Combine(_updateTempPath, "AgentUpdater.exe");
            
            // If updater doesn't exist in install, check extracted package
            if (!File.Exists(updaterSource))
            {
                updaterSource = Path.Combine(extractPath, "AgentUpdater.exe");
            }
            
            if (File.Exists(updaterSource))
            {
                File.Copy(updaterSource, updaterDest, true);
                // Unblock the updater too
                UnblockFile(updaterDest);
            }
            else
            {
                _logger.LogError("AgentUpdater.exe not found. Cannot apply update.");
                await ReportProgressAsync(AgentUpdateStatus.Failed, 0, "AgentUpdater.exe not found");
                return false;
            }

            // Save server URL and API key to temp for the updater to use
            // Also pass reinstall info if needed
            await SaveUpdaterConfigAsync(updateInfo);

            // Launch the updater process with retry logic
            var updaterLaunched = false;
            var maxRetries = 3;
            Process? process = null;
            
            for (int attempt = 1; attempt <= maxRetries && !updaterLaunched; attempt++)
            {
                try
                {
                    _logger.LogInformation("Launching updater (attempt {Attempt}/{MaxRetries}): {Updater}", 
                        attempt, maxRetries, updaterDest);
                    
                    // IMPORTANT: Do NOT redirect stdout/stderr - the updater writes logs to console
                    // and if we redirect but don't read, the pipe buffer fills up and the updater hangs!
                    // The updater logs everything to C:\ProgramData\ComputerInventory\updater.log anyway.
                    // NOTE: Remove trailing backslash from paths to avoid escaping the closing quote
                    var installPathClean = _installPath.TrimEnd('\\', '/');
                    var extractPathClean = extractPath.TrimEnd('\\', '/');
                    
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = updaterDest,
                        Arguments = $"\"{extractPathClean}\" \"{installPathClean}\" \"ComputerInventoryAgent\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = false,
                        RedirectStandardError = false,
                        WorkingDirectory = _updateTempPath
                    };

                    await ReportProgressAsync(AgentUpdateStatus.PreparingUpdater, 80 + attempt, 
                        $"Launching updater (attempt {attempt}/{maxRetries})...");
                    
                    // Use HVCI-safe process launch with timeout to prevent hangs
                    process = await HvciSafeProcess.StartWithTimeoutAsync(processInfo, TimeSpan.FromSeconds(30));
                    
                    if (process != null)
                    {
                        // Wait briefly to see if process started successfully
                        await Task.Delay(500);
                        if (!process.HasExited)
                        {
                            updaterLaunched = true;
                            _logger.LogInformation("Updater process started successfully (PID: {PID})", process.Id);
                        }
                        else
                        {
                            var exitCode = process.ExitCode;
                            _logger.LogWarning("Updater exited immediately with code {ExitCode}. Check C:\\ProgramData\\ComputerInventory\\updater.log for details.", 
                                exitCode);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Process.Start returned null on attempt {Attempt}", attempt);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to launch updater on attempt {Attempt}", attempt);
                }
                
                if (!updaterLaunched && attempt < maxRetries)
                {
                    await Task.Delay(1000 * attempt); // Exponential backoff
                }
            }
            
            if (!updaterLaunched)
            {
                _logger.LogError("Failed to start updater process after {MaxRetries} attempts", maxRetries);
                await ReportProgressAsync(AgentUpdateStatus.Failed, 0, "Failed to start updater after retries");
                return false;
            }
            
            _logger.LogInformation("Updater launched successfully. Service will restart with new version.");
            await ReportProgressAsync(AgentUpdateStatus.PreparingUpdater, 85, "Updater running - service will restart...");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download and apply update");
            await ReportProgressAsync(AgentUpdateStatus.Failed, 0, $"Update failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Save configuration for the updater to use for progress reporting.
    /// Includes fallback URLs for clients outside the local network.
    /// Also includes ReinstallRequired flag if the server indicated a full reinstall is needed.
    /// </summary>
    private async Task SaveUpdaterConfigAsync(UpdateInfo updateInfo)
    {
        try
        {
            var configPath = Path.Combine(_updateTempPath, "updater-config.json");
            
            // Get fallback URLs from configuration
            var fallbackUrls = _configuration.GetSection("ApiSettings:FallbackUrls").Get<string[]>() ?? Array.Empty<string>();
            
            var config = new
            {
                ServerUrl = _configuration["ApiSettings:BaseUrl"] ?? "",
                FallbackUrls = fallbackUrls,
                ApiKey = _apiKey,
                SerialNumber = _serialNumber ?? await GetSerialNumberAsync(),
                AgentSessionId = _connectionManager.AgentSessionId,
                // Reinstall support - tells the updater to run the install script instead of normal update
                ReinstallRequired = updateInfo.ReinstallRequired,
                InstallScriptUrl = updateInfo.InstallScriptUrl,
                MaxBackupsToKeep = _configuration.GetValue("AgentSettings:MaxBackupsToKeep", 1)
            };
            
            if (updateInfo.ReinstallRequired)
            {
                _logger.LogWarning("\ud83d\udd27 ReinstallRequired flag is set - updater will perform full reinstall");
            }
            
            var json = System.Text.Json.JsonSerializer.Serialize(config);
            await File.WriteAllTextAsync(configPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save updater config");
        }
    }

    private async Task<bool> DownloadUpdatePackageAsync(UpdateInfo updateInfo, string destinationPath, CancellationToken cancellationToken)
    {
        const int maxResumeAttempts = 3;
        var maxDownloadSpeedKbps = _configuration.GetValue<int>("AgentSettings:MaxDownloadSpeedKbps", 0);

        if (maxDownloadSpeedKbps > 0)
        {
            _logger.LogInformation("Bandwidth throttling enabled: {Speed} KB/s", maxDownloadSpeedKbps);
        }

        for (int attempt = 1; attempt <= maxResumeAttempts; attempt++)
        {
            try
            {
                var client = CreateClient();
                client.Timeout = TimeSpan.FromMinutes(10);

                // Check for existing partial download to resume
                long existingBytes = 0;
                if (File.Exists(destinationPath))
                {
                    existingBytes = new FileInfo(destinationPath).Length;
                }

                // If we already have the complete file, skip download
                if (existingBytes > 0 && updateInfo.FileSize > 0 && existingBytes == updateInfo.FileSize)
                {
                    _logger.LogInformation("Download already complete ({Bytes} bytes), skipping download", existingBytes);
                    await ReportProgressAsync(AgentUpdateStatus.Downloading, 50, $"Download already complete ({existingBytes / (1024 * 1024.0):F1}MB)");
                    return true;
                }

                // Set Range header to resume from where we left off
                var request = new HttpRequestMessage(HttpMethod.Get, updateInfo.DownloadUrl);
                if (existingBytes > 0)
                {
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existingBytes, null);
                    _logger.LogInformation("Resuming download from byte {Offset} (attempt {Attempt}/{Max})...", existingBytes, attempt, maxResumeAttempts);
                    await ReportProgressAsync(AgentUpdateStatus.Downloading, 10, $"Resuming download from {existingBytes / (1024 * 1024.0):F1}MB (attempt {attempt})...");
                }
                else
                {
                    _logger.LogInformation("Downloading update from {Url} (attempt {Attempt}/{Max})...", updateInfo.DownloadUrl, attempt, maxResumeAttempts);
                    await ReportProgressAsync(AgentUpdateStatus.Downloading, 10, "Starting download...");
                }

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                // Handle 416 Range Not Satisfiable — partial file is stale (server file changed or is smaller)
                if (existingBytes > 0 && response.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    _logger.LogWarning("Server returned 416 Range Not Satisfiable — partial file is stale. Deleting partial download and restarting.");
                    response.Dispose();
                    File.Delete(destinationPath);
                    await ReportProgressAsync(AgentUpdateStatus.Downloading, 10, "Partial download invalid, restarting from scratch...");
                    continue; // retry loop will start a fresh download
                }

                long totalBytes;
                FileMode fileMode;
                long bytesAlreadyCounted;

                if (existingBytes > 0 && response.StatusCode == System.Net.HttpStatusCode.PartialContent)
                {
                    // Server supports range requests — append to existing file
                    var contentRange = response.Content.Headers.ContentRange;
                    totalBytes = contentRange?.Length ?? updateInfo.FileSize;
                    fileMode = FileMode.Append;
                    bytesAlreadyCounted = existingBytes;
                    _logger.LogInformation("Server accepted range request — resuming from {Offset}/{Total} bytes", existingBytes, totalBytes);
                }
                else
                {
                    // Full response (200) — server doesn't support range or this is a fresh download
                    response.EnsureSuccessStatusCode();
                    totalBytes = response.Content.Headers.ContentLength ?? updateInfo.FileSize;
                    fileMode = FileMode.Create;
                    bytesAlreadyCounted = 0;

                    if (existingBytes > 0)
                    {
                        _logger.LogWarning("Server does not support range requests — restarting full download");
                    }
                }

                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);

                // Apply bandwidth throttling if configured
                Stream readStream = contentStream;
                if (maxDownloadSpeedKbps > 0)
                {
                    readStream = new ThrottledStream(contentStream, maxDownloadSpeedKbps * 1024L);
                }

                await using var throttledStreamOwner = maxDownloadSpeedKbps > 0 ? readStream as IAsyncDisposable : null;
                await using var fileStream = new FileStream(destinationPath, fileMode, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                var totalRead = bytesAlreadyCounted;
                var lastProgressReport = totalBytes > 0 ? (int)((bytesAlreadyCounted * 100) / totalBytes) / 10 * 10 : 0;
                int bytesRead;

                while ((bytesRead = await readStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalRead += bytesRead;

                    if (totalBytes > 0)
                    {
                        var downloadPercent = (int)((totalRead * 100) / totalBytes);
                        var overallProgress = 10 + (downloadPercent * 40 / 100);

                        if (downloadPercent >= lastProgressReport + 10)
                        {
                            _logger.LogInformation("Download progress: {Progress}%", downloadPercent);
                            var sizeDownloaded = totalRead / (1024 * 1024.0);
                            var totalSize = totalBytes / (1024 * 1024.0);
                            await ReportProgressAsync(AgentUpdateStatus.Downloading, overallProgress,
                                $"Downloading... {downloadPercent}% ({sizeDownloaded:F1}MB / {totalSize:F1}MB)");
                            lastProgressReport = downloadPercent;
                        }
                    }
                }

                _logger.LogInformation("Download complete: {Bytes} bytes", totalRead);
                await ReportProgressAsync(AgentUpdateStatus.Downloading, 50, $"Download complete ({totalRead / (1024 * 1024.0):F1}MB)");
                return true;
            }
            catch (Exception ex) when (attempt < maxResumeAttempts && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Download interrupted on attempt {Attempt}/{Max} — will retry with resume", attempt, maxResumeAttempts);
                await ReportProgressAsync(AgentUpdateStatus.Downloading, 10, $"Download interrupted, retrying (attempt {attempt + 1}/{maxResumeAttempts})...");
                await Task.Delay(2000 * attempt, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download update package");
                return false;
            }
        }

        _logger.LogError("Failed to download update package after {Max} attempts", maxResumeAttempts);
        return false;
    }

    private async Task<bool> VerifyChecksumAsync(string filePath, string expectedChecksum)
    {
        try
        {
            using var sha256 = SHA256.Create();
            await using var stream = File.OpenRead(filePath);
            var hash = await sha256.ComputeHashAsync(stream);
            var actualChecksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            
            var isValid = string.Equals(actualChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase);
            
            if (!isValid)
            {
                _logger.LogError("Checksum mismatch. Expected: {Expected}, Actual: {Actual}", 
                    expectedChecksum, actualChecksum);
            }
            
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify checksum");
            return false;
        }
    }

    /// <summary>
    /// Unblocks all files in a directory by removing the Zone.Identifier alternate data stream.
    /// This is necessary for systems with Code Integrity enforcement (HVCI/WDAC).
    /// </summary>
    private void UnblockFilesInDirectory(string directoryPath)
    {
        try
        {
            _logger.LogInformation("Unblocking files in {Directory}", directoryPath);
            
            foreach (var file in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                UnblockFile(file);
            }
            
            _logger.LogInformation("Finished unblocking files in {Directory}", directoryPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error while unblocking files in {Directory}", directoryPath);
        }
    }

    /// <summary>
    /// Unblocks a single file by removing the Zone.Identifier alternate data stream.
    /// </summary>
    private void UnblockFile(string filePath)
    {
        try
        {
            var zoneIdentifierPath = filePath + ":Zone.Identifier";
            
            // Use native Win32 API to delete the alternate data stream
            // File.Delete doesn't work on ADS, we need to use DeleteFile from kernel32
            if (NativeDeleteFile(zoneIdentifierPath))
            {
                _logger.LogDebug("Unblocked file: {File}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not unblock file {File} (may not have Zone.Identifier)", filePath);
        }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool NativeDeleteFile(string lpFileName);
}
