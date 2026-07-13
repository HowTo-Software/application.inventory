using ComputerInventory.Core.Interfaces;
using ComputerInventory.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace ComputerInventory.Infrastructure.Repositories;

public class DatabaseMaintenanceRepository : IDatabaseMaintenanceRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DatabaseMaintenanceRepository> _logger;

    public DatabaseMaintenanceRepository(ApplicationDbContext context, ILogger<DatabaseMaintenanceRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<int> MarkOfflineComputersAsync(int offlineThresholdMinutes, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpScalarAsync<int?>(
            "sp_mark_offline_computers",
            cancellationToken,
            SpParam.Input("@offline_threshold_minutes", offlineThresholdMinutes)) ?? 0;
    }

    public async Task<int> CleanupOldSystemDetailsAsync(int retentionDays, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpScalarAsync<int?>(
            "sp_cleanup_old_system_details",
            cancellationToken,
            SpParam.Input("@retention_days", retentionDays)) ?? 0;
    }

    public async Task<int> CleanupExcessRecordsAsync(string tableName, int maxRecordsPerComputer, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpScalarAsync<int?>(
            "sp_cleanup_excess_records",
            cancellationToken,
            SpParam.Input("@table_name", tableName),
            SpParam.Input("@max_records_per_computer", maxRecordsPerComputer)) ?? 0;
    }

    public async Task<int> CleanupOldRestorePointsAsync(int retentionDays, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpScalarAsync<int?>(
            "sp_cleanup_old_restore_points",
            cancellationToken,
            SpParam.Input("@retention_days", retentionDays)) ?? 0;
    }

    public async Task<int> CleanupOldHistoryRecordsAsync(int retentionDays, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpScalarAsync<int?>(
            "sp_cleanup_old_history_records",
            cancellationToken,
            SpParam.Input("@retention_days", retentionDays)) ?? 0;
    }

    public async Task<int> MarkStuckUpdatesAsFailedAsync(int timeoutHours, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpScalarAsync<int?>(
            "sp_mark_stuck_updates_as_failed",
            cancellationToken,
            SpParam.Input("@timeout_hours", timeoutHours)) ?? 0;
    }
}
