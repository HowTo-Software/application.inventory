using System.Data;
using System.Data.Common;
using System.IO.Compression;
using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using ComputerInventory.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace ComputerInventory.Infrastructure.Repositories;

public class RestorePointRepository : IRestorePointRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RestorePointRepository> _logger;

    public RestorePointRepository(ApplicationDbContext context, ILogger<RestorePointRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public IQueryable<RestorePoint> Query => _context.RestorePoints.AsNoTracking();

    public async Task<RestorePoint?> GetByGuidAsync(Guid restorePointId, CancellationToken cancellationToken = default)
    {
        return await _context.ExecuteSpSingleAsync<RestorePoint>(
            "sp_get_restore_point_by_guid", cancellationToken,
            SpParam.Input("@restore_point_id", restorePointId));
    }

    public async Task<RestorePoint?> GetByGuidWithDetailsAsync(Guid restorePointId, CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_restore_point_by_guid_with_details] @restore_point_id";
            command.Parameters.Add(new SqlParameter("@restore_point_id", SqlDbType.UniqueIdentifier) { Value = restorePointId });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // RS1: RestorePoint + Computer + Type
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            var rp = MapRestorePointFromReader(reader);
            rp.Computer = new Computer
            {
                Id = rp.ComputerId,
                Hostname = reader["_computer_hostname"] as string ?? string.Empty,
                SerialNumber = reader["_computer_serial_number"] as string ?? string.Empty,
            };
            rp.TypeLookup = new RestorePointTypeLookup
            {
                Id = rp.TypeId,
                Name = reader["_type_name"] as string ?? string.Empty,
            };

            // RS2: SystemInfo
            if (await reader.NextResultAsync(cancellationToken) && await reader.ReadAsync(cancellationToken))
                rp.SystemInfo = MapSystemInfoFromReader(reader);

            // RS3: Snapshot (GZip-compressed VARBINARY)
            if (await reader.NextResultAsync(cancellationToken) && await reader.ReadAsync(cancellationToken))
                rp.Snapshot = MapSnapshotFromReader(reader);

            return rp;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<List<RestorePoint>> GetByComputerIdAsync(int computerId, CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_restore_points_by_computer_id] @computer_id";
            command.Parameters.Add(new SqlParameter("@computer_id", SqlDbType.Int) { Value = computerId });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // RS1: RestorePoints
            var items = new List<RestorePoint>();
            while (await reader.ReadAsync(cancellationToken))
                items.Add(MapRestorePointFromReader(reader));

            // RS2: SystemInfos — correlate by restore_point_id (FK)
            if (await reader.NextResultAsync(cancellationToken))
            {
                var siMap = new Dictionary<int, RestorePointSystemInfo>();
                while (await reader.ReadAsync(cancellationToken))
                {
                    var si = MapSystemInfoFromReader(reader);
                    siMap[si.RestorePointId] = si;
                }
                foreach (var rp in items)
                {
                    if (siMap.TryGetValue(rp.Id, out var si))
                        rp.SystemInfo = si;
                }
            }

            return items;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<RestorePoint?> GetLatestCompleteByComputerIdAsync(int computerId, CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_latest_complete_restore_point] @computer_id";
            command.Parameters.Add(new SqlParameter("@computer_id", SqlDbType.Int) { Value = computerId });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // RS1: RestorePoint
            if (!await reader.ReadAsync(cancellationToken))
                return null;
            var rp = MapRestorePointFromReader(reader);

            // RS2: SystemInfo
            if (await reader.NextResultAsync(cancellationToken) && await reader.ReadAsync(cancellationToken))
                rp.SystemInfo = MapSystemInfoFromReader(reader);

            // RS3: Snapshot
            if (await reader.NextResultAsync(cancellationToken) && await reader.ReadAsync(cancellationToken))
                rp.Snapshot = MapSnapshotFromReader(reader);

            return rp;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task AddAsync(RestorePoint restorePoint, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.RestorePoints.Add(restorePoint);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add restore point {RestorePointId} for computer {ComputerId}", restorePoint.RestorePointId, restorePoint.ComputerId);
            throw;
        }
    }

    public async Task DeleteAsync(RestorePoint restorePoint, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.RestorePoints.Remove(restorePoint);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete restore point {RestorePointId}", restorePoint.RestorePointId);
            throw;
        }
    }

    public async Task<int> CleanupOldRestorePointsAsync(int computerId, int keepCount, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.ExecuteSpNonQueryAsync(
                "sp_cleanup_old_restore_points_by_computer",
                cancellationToken,
                SpParam.Input("@computer_id", computerId),
                SpParam.Input("@keep_count", keepCount));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup restore points for computer {ComputerId}, keeping {KeepCount}", computerId, keepCount);
            throw;
        }
    }

    public async Task<(List<RestorePointSummaryItem> Items, int TotalCount)> GetSummaryAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_restore_point_summary] @page, @page_size";
            command.Parameters.Add(new SqlParameter("@page", page));
            command.Parameters.Add(new SqlParameter("@page_size", pageSize));

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // Result set 1: total count
            int totalCount = 0;
            if (await reader.ReadAsync(cancellationToken))
                totalCount = reader.GetInt32(0);

            // Result set 2: paged summary rows
            var items = new List<RestorePointSummaryItem>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                    items.Add(MapSummaryFromReader(reader));
            }

            return (items, totalCount);
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    private static RestorePointSummaryItem MapSummaryFromReader(DbDataReader r) => new()
    {
        ComputerId = (int)r["computer_id"],
        Hostname = r["hostname"] as string ?? "Unknown",
        SerialNumber = r["serial_number"] as string ?? "Unknown",
        TotalRestorePoints = (int)r["total_restore_points"],
        LatestRestorePoint = (DateTime)r["latest_restore_point"],
        OldestRestorePoint = (DateTime)r["oldest_restore_point"],
        TotalSizeBytes = (long)r["total_size_bytes"],
        RebootCount = (int)r["reboot_count"]
    };

    private static RestorePoint MapRestorePointFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        ComputerId = (int)r["computer_id"],
        RestorePointId = (Guid)r["restore_point_id"],
        CreatedAt = (DateTime)r["created_at"],
        TypeId = (int)r["type_id"],
        Description = r["description"] as string,
        DataSizeBytes = r["data_size_bytes"] is DBNull ? null : (long)r["data_size_bytes"],
        DataHash = r["data_hash"] as string,
        IsComplete = (bool)r["is_complete"],
        ErrorMessage = r["error_message"] as string,
        IsDeleted = (bool)r["is_deleted"],
        DeletedAt = r["deleted_at"] is DBNull ? null : (DateTime)r["deleted_at"],
    };

    private static RestorePointSystemInfo MapSystemInfoFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        RestorePointId = (int)r["restore_point_id"],
        SystemBootTime = r["system_boot_time"] is DBNull ? null : (DateTime)r["system_boot_time"],
        UptimeSeconds = r["uptime_seconds"] is DBNull ? null : (long)r["uptime_seconds"],
        Hostname = r["hostname"] as string,
        SerialNumber = r["serial_number"] as string,
        IpAddress = r["ip_address"] as string,
        MacAddress = r["mac_address"] as string,
        Manufacturer = r["manufacturer"] as string,
        Model = r["model"] as string,
        CurrentUser = r["current_user"] as string,
        CreatedAt = (DateTime)r["created_at"],
        UpdatedAt = r["updated_at"] is DBNull ? null : (DateTime)r["updated_at"],
    };

    private static RestorePointSnapshot MapSnapshotFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        RestorePointId = (int)r["restore_point_id"],
        SystemDetailsJson = DecompressGzip(r["system_details_json"]),
        InstalledSoftwareJson = DecompressGzip(r["installed_software_json"]),
        RunningProcessesJson = DecompressGzip(r["running_processes_json"]),
        WindowsServicesJson = DecompressGzip(r["windows_services_json"]),
        StartupProgramsJson = DecompressGzip(r["startup_programs_json"]),
        ScheduledTasksJson = DecompressGzip(r["scheduled_tasks_json"]),
        EnvironmentVariablesJson = DecompressGzip(r["environment_variables_json"]),
        NetworkConfigJson = DecompressGzip(r["network_config_json"]),
        StorageConfigJson = DecompressGzip(r["storage_config_json"]),
        EventLogSummaryJson = DecompressGzip(r["event_log_summary_json"]),
        WindowsUpdatesJson = DecompressGzip(r["windows_updates_json"]),
        DeviceDriversJson = DecompressGzip(r["device_drivers_json"]),
        FirewallRulesJson = DecompressGzip(r["firewall_rules_json"]),
        LocalUsersJson = DecompressGzip(r["local_users_json"]),
        LocalGroupsJson = DecompressGzip(r["local_groups_json"]),
        SharedFoldersJson = DecompressGzip(r["shared_folders_json"]),
        PrintersJson = DecompressGzip(r["printers_json"]),
        UsbDevicesJson = DecompressGzip(r["usb_devices_json"]),
        BatteryStatusJson = DecompressGzip(r["battery_status_json"]),
        BitLockerStatusJson = DecompressGzip(r["bit_locker_status_json"]),
        SecurityStatusJson = DecompressGzip(r["security_status_json"]),
        CreatedAt = (DateTime)r["created_at"],
        UpdatedAt = r["updated_at"] is DBNull ? null : (DateTime)r["updated_at"],
    };

    private static string? DecompressGzip(object value)
    {
        if (value is DBNull || value is not byte[] bytes || bytes.Length == 0)
            return null;

        using var input = new MemoryStream(bytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return System.Text.Encoding.UTF8.GetString(output.ToArray());
    }
}
