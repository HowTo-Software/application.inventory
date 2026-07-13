using System.Diagnostics;
using ComputerInventory.Core.Interfaces;

namespace ComputerInventory.Api.Services;

/// <summary>
/// Background service that performs periodic database maintenance tasks
/// including cleanup of old data, updating statistics, and marking offline computers
/// </summary>
public class DatabaseMaintenanceService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseMaintenanceService> _logger;
    private readonly IConfiguration _configuration;
    private readonly AgentFileSettings _fileSettings;
    
    // Maintenance intervals
    private readonly TimeSpan _offlineCheckInterval;
    private readonly TimeSpan _cleanupInterval;
    private readonly int _offlineThresholdMinutes;
    private readonly int _systemDetailsRetentionDays;
    private readonly int _restorePointRetentionDays;
    private readonly int _historyRetentionDays;
    private readonly int _maxSystemDetailsPerComputer;
    private readonly int _maxRestorePointsPerComputer;
    private readonly int _stuckUpdateTimeoutHours;

    public DatabaseMaintenanceService(
        IServiceProvider serviceProvider,
        ILogger<DatabaseMaintenanceService> logger,
        IConfiguration configuration,
        AgentFileSettings fileSettings)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        _fileSettings = fileSettings;
        
        // Load configuration with defaults
        var maintenanceConfig = _configuration.GetSection("DatabaseMaintenance");
        _offlineCheckInterval = TimeSpan.FromMinutes(maintenanceConfig.GetValue("OfflineCheckIntervalMinutes", 1));
        _cleanupInterval = TimeSpan.FromHours(maintenanceConfig.GetValue("CleanupIntervalHours", 24));
        _offlineThresholdMinutes = maintenanceConfig.GetValue("OfflineThresholdMinutes", 5);
        _systemDetailsRetentionDays = ClampWithWarning(maintenanceConfig.GetValue("SystemDetailsRetentionDays", 30), 1, 3650, nameof(_systemDetailsRetentionDays));
        _restorePointRetentionDays = ClampWithWarning(maintenanceConfig.GetValue("RestorePointRetentionDays", 90), 1, 3650, nameof(_restorePointRetentionDays));
        _historyRetentionDays = ClampWithWarning(maintenanceConfig.GetValue("HistoryRetentionDays", 365), 1, 3650, nameof(_historyRetentionDays));
        _maxSystemDetailsPerComputer = ClampWithWarning(maintenanceConfig.GetValue("MaxSystemDetailsPerComputer", 100), 1, 10000, nameof(_maxSystemDetailsPerComputer));
        _maxRestorePointsPerComputer = ClampWithWarning(maintenanceConfig.GetValue("MaxRestorePointsPerComputer", 50), 1, 10000, nameof(_maxRestorePointsPerComputer));
        _stuckUpdateTimeoutHours = ClampWithWarning(maintenanceConfig.GetValue("StuckUpdateTimeoutHours", 2), 1, 168, nameof(_stuckUpdateTimeoutHours));
    }

    private int ClampWithWarning(int value, int min, int max, string settingName)
    {
        if (value < min || value > max)
        {
            var clamped = Math.Clamp(value, min, max);
            _logger.LogWarning("DatabaseMaintenance setting {Setting} value {Value} is out of range [{Min}–{Max}], clamped to {Clamped}",
                settingName, value, min, max, clamped);
            return clamped;
        }
        return value;
    }

    private const string LastCleanupKey = "DatabaseMaintenance:LastCleanupUtc";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Database Maintenance Service starting...");
        
        // Run initial offline check after short delay
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        
        var lastCleanup = await LoadLastCleanupAsync(stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Always check for offline computers
                await MarkOfflineComputersAsync(stoppingToken);
                
                // Run cleanup on schedule
                if (DateTime.UtcNow - lastCleanup > _cleanupInterval)
                {
                    await PerformCleanupAsync(stoppingToken);
                    lastCleanup = DateTime.UtcNow;
                    await SaveLastCleanupAsync(lastCleanup, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown, don't log as error
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during database maintenance");
            }
            
            await Task.Delay(_offlineCheckInterval, stoppingToken);
        }
        
        _logger.LogInformation("Database Maintenance Service stopped");
    }

    private async Task<DateTime> LoadLastCleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var appSettingRepository = scope.ServiceProvider.GetRequiredService<IAppSettingRepository>();
            var setting = await appSettingRepository.GetByKeyAsync(LastCleanupKey, cancellationToken);

            if (setting != null && DateTime.TryParse(setting.Value, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            {
                _logger.LogInformation("Loaded persisted last cleanup time: {LastCleanup}", parsed);
                return parsed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load last cleanup time from database — will run cleanup on this cycle");
        }

        return DateTime.MinValue;
    }

    private async Task SaveLastCleanupAsync(DateTime lastCleanup, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var appSettingRepository = scope.ServiceProvider.GetRequiredService<IAppSettingRepository>();
            await appSettingRepository.SetValueAsync(LastCleanupKey, lastCleanup.ToString("O"), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist last cleanup time — cleanup will re-run if app restarts before next interval");
        }
    }

    /// <summary>
    /// Mark computers as offline if they haven't checked in recently
    /// </summary>
    private async Task MarkOfflineComputersAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var maintenanceRepository = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenanceRepository>();
        
        var updatedCount = await maintenanceRepository.MarkOfflineComputersAsync(_offlineThresholdMinutes, cancellationToken);
        
        if (updatedCount > 0)
        {
            _logger.LogInformation("Marked {Count} computers as offline (no check-in for {Minutes} minutes)", 
                updatedCount, _offlineThresholdMinutes);
        }
    }

    /// <summary>
    /// Perform comprehensive cleanup of old data
    /// </summary>
    private async Task PerformCleanupAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting database cleanup...");
        var overallStopwatch = Stopwatch.StartNew();
        var taskStopwatch = new Stopwatch();
        
        using var scope = _serviceProvider.CreateScope();
        var maintenanceRepository = scope.ServiceProvider.GetRequiredService<IDatabaseMaintenanceRepository>();
        
        var totalDeleted = 0;
        var failedTasks = 0;
        
        // 1. Clean up old system details
        try
        {
            taskStopwatch.Restart();
            var deletedSystemDetails = await maintenanceRepository.CleanupOldSystemDetailsAsync(_systemDetailsRetentionDays, cancellationToken);
            taskStopwatch.Stop();
            totalDeleted += deletedSystemDetails;
            
            _logger.LogInformation("Cleanup task 1: Deleted {Count} old system detail records in {ElapsedMs}ms (retention: {Days} days)", 
                deletedSystemDetails, taskStopwatch.ElapsedMilliseconds, _systemDetailsRetentionDays);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            taskStopwatch.Stop();
            failedTasks++;
            _logger.LogError(ex, "Cleanup task 1 failed after {ElapsedMs}ms: Clean up old system details", taskStopwatch.ElapsedMilliseconds);
        }
        
        // 2. Clean up excess system details per computer (keep most recent)
        try
        {
            taskStopwatch.Restart();
            var excessSystemDetails = await maintenanceRepository.CleanupExcessRecordsAsync("system_details", _maxSystemDetailsPerComputer, cancellationToken);
            taskStopwatch.Stop();
            totalDeleted += excessSystemDetails;
            
            _logger.LogInformation("Cleanup task 2: Deleted {Count} excess system detail records in {ElapsedMs}ms (limit: {Max}/computer)",
                excessSystemDetails, taskStopwatch.ElapsedMilliseconds, _maxSystemDetailsPerComputer);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            taskStopwatch.Stop();
            failedTasks++;
            _logger.LogError(ex, "Cleanup task 2 failed after {ElapsedMs}ms: Clean up excess system details per computer", taskStopwatch.ElapsedMilliseconds);
        }
        
        // 3. Clean up old restore points
        try
        {
            taskStopwatch.Restart();
            var deletedRestorePoints = await maintenanceRepository.CleanupOldRestorePointsAsync(_restorePointRetentionDays, cancellationToken);
            taskStopwatch.Stop();
            totalDeleted += deletedRestorePoints;
            
            _logger.LogInformation("Cleanup task 3: Deleted {Count} old restore points in {ElapsedMs}ms (retention: {Days} days)", 
                deletedRestorePoints, taskStopwatch.ElapsedMilliseconds, _restorePointRetentionDays);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            taskStopwatch.Stop();
            failedTasks++;
            _logger.LogError(ex, "Cleanup task 3 failed after {ElapsedMs}ms: Clean up old restore points", taskStopwatch.ElapsedMilliseconds);
        }
        
        // 4. Clean up excess restore points per computer
        try
        {
            taskStopwatch.Restart();
            var excessRestorePoints = await maintenanceRepository.CleanupExcessRecordsAsync("restore_points", _maxRestorePointsPerComputer, cancellationToken);
            taskStopwatch.Stop();
            totalDeleted += excessRestorePoints;
            
            _logger.LogInformation("Cleanup task 4: Deleted {Count} excess restore points in {ElapsedMs}ms (limit: {Max}/computer)",
                excessRestorePoints, taskStopwatch.ElapsedMilliseconds, _maxRestorePointsPerComputer);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            taskStopwatch.Stop();
            failedTasks++;
            _logger.LogError(ex, "Cleanup task 4 failed after {ElapsedMs}ms: Clean up excess restore points per computer", taskStopwatch.ElapsedMilliseconds);
        }
        
        // 5. Clean up old history records
        try
        {
            taskStopwatch.Restart();
            var deletedHistory = await maintenanceRepository.CleanupOldHistoryRecordsAsync(_historyRetentionDays, cancellationToken);
            taskStopwatch.Stop();
            totalDeleted += deletedHistory;
            
            _logger.LogInformation("Cleanup task 5: Deleted {Count} old history records in {ElapsedMs}ms (retention: {Days} days)", 
                deletedHistory, taskStopwatch.ElapsedMilliseconds, _historyRetentionDays);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            taskStopwatch.Stop();
            failedTasks++;
            _logger.LogError(ex, "Cleanup task 5 failed after {ElapsedMs}ms: Clean up old history records", taskStopwatch.ElapsedMilliseconds);
        }
        
        // 6. Clean up stale update statuses (stuck updates)
        try
        {
            taskStopwatch.Restart();
            var stuckUpdates = await maintenanceRepository.MarkStuckUpdatesAsFailedAsync(_stuckUpdateTimeoutHours, cancellationToken);
            taskStopwatch.Stop();
            
            _logger.LogInformation("Cleanup task 6: Marked {Count} stuck updates as failed in {ElapsedMs}ms (timeout: {Hours}h)",
                stuckUpdates, taskStopwatch.ElapsedMilliseconds, _stuckUpdateTimeoutHours);
            if (stuckUpdates > 0)
            {
                _logger.LogWarning("Marked {Count} stuck updates as failed (started over {Hours} hours ago)", stuckUpdates, _stuckUpdateTimeoutHours);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            taskStopwatch.Stop();
            failedTasks++;
            _logger.LogError(ex, "Cleanup task 6 failed after {ElapsedMs}ms: Clean up stale update statuses", taskStopwatch.ElapsedMilliseconds);
        }
        
        // 7. Clean up old archived update packages from filesystem
        taskStopwatch.Restart();
        CleanupOldUpdatePackages();
        taskStopwatch.Stop();
        _logger.LogInformation("Cleanup task 7: Update package cleanup completed in {ElapsedMs}ms", taskStopwatch.ElapsedMilliseconds);
        
        overallStopwatch.Stop();
        var totalElapsed = overallStopwatch.ElapsedMilliseconds;
        
        if (failedTasks > 0)
        {
            _logger.LogWarning("Database cleanup completed in {ElapsedMs}ms with {FailedTasks} failed task(s). Total records deleted: {Total}", 
                totalElapsed, failedTasks, totalDeleted);
        }
        else
        {
            _logger.LogInformation("Database cleanup completed in {ElapsedMs}ms. Total records deleted: {Total}", 
                totalElapsed, totalDeleted);
        }
        
        if (totalElapsed > 60_000)
        {
            _logger.LogWarning("Database cleanup took {ElapsedSeconds}s — exceeds 60s threshold. Investigate slow tasks above.",
                totalElapsed / 1000.0);
        }
    }

    /// <summary>
    /// Clean up old/archived update packages from the agent update directory.
    /// Keeps only the current agent-update.zip and update.json.
    /// Removes any leftover temp files, old zips, or orphaned artifacts.
    /// </summary>
    private void CleanupOldUpdatePackages()
    {
        try
        {
            var updateDir = _fileSettings.GetUpdatePath();
            if (!Directory.Exists(updateDir))
                return;

            var allowedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                _fileSettings.UpdatePackageFileName,
                _fileSettings.UpdateJsonFileName
            };

            var deletedCount = 0;

            // Remove unexpected files (old archives, temp files, etc.)
            foreach (var file in Directory.GetFiles(updateDir))
            {
                var fileName = Path.GetFileName(file);
                if (!allowedFiles.Contains(fileName))
                {
                    try
                    {
                        File.Delete(file);
                        deletedCount++;
                        _logger.LogInformation("Cleaned up unexpected file in update directory: {File}", fileName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not delete file {File} from update directory", fileName);
                    }
                }
            }

            // Remove any subdirectories (temp extraction leftovers)
            foreach (var dir in Directory.GetDirectories(updateDir))
            {
                try
                {
                    Directory.Delete(dir, true);
                    deletedCount++;
                    _logger.LogInformation("Cleaned up directory in update path: {Dir}", Path.GetFileName(dir));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete directory {Dir} from update path", Path.GetFileName(dir));
                }
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation("Update package cleanup removed {Count} item(s)", deletedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update package cleanup failed");
        }
    }

}
