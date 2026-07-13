namespace ComputerInventory.Core.Interfaces;

public interface IDatabaseMaintenanceRepository
{
    Task<int> MarkOfflineComputersAsync(int offlineThresholdMinutes, CancellationToken cancellationToken = default);
    Task<int> CleanupOldSystemDetailsAsync(int retentionDays, CancellationToken cancellationToken = default);
    Task<int> CleanupExcessRecordsAsync(string tableName, int maxRecordsPerComputer, CancellationToken cancellationToken = default);
    Task<int> CleanupOldRestorePointsAsync(int retentionDays, CancellationToken cancellationToken = default);
    Task<int> CleanupOldHistoryRecordsAsync(int retentionDays, CancellationToken cancellationToken = default);
    Task<int> MarkStuckUpdatesAsFailedAsync(int timeoutHours, CancellationToken cancellationToken = default);
}
