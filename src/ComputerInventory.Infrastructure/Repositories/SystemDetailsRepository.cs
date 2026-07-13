using System.Data;
using System.Data.Common;
using ComputerInventory.Contracts;
using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;
using ComputerInventory.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

using static ComputerInventory.Infrastructure.Data.SystemDetailsQueryExtensions;
using static ComputerInventory.Infrastructure.Repositories.SystemDetailsMappers;

namespace ComputerInventory.Infrastructure.Repositories;

public class SystemDetailsRepository : ISystemDetailsRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SystemDetailsRepository> _logger;

    public SystemDetailsRepository(ApplicationDbContext context, ILogger<SystemDetailsRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public IQueryable<SystemDetails> Query => _context.SystemDetails.AsNoTracking();

    public async Task AddAsync(SystemDetails details, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.SystemDetails.Add(details);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add system details for computer {ComputerId}", details.ComputerId);
            throw;
        }
    }

    public async Task<SystemDetails?> GetLatestByComputerIdAsync(int computerId, CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_latest_system_details_by_computer_id] @computer_id";
            command.Parameters.Add(new SqlParameter("@computer_id", SqlDbType.Int) { Value = computerId });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // RS1: SystemDetails + Computer
            if (!await reader.ReadAsync(cancellationToken))
                return null;
            var sd = MapSystemDetailsWithComputerFromReader(reader);

            // RS2-RS17: All 16 sub-entities (single record each)
            await ReadAllSubEntitiesSingle(reader, sd, cancellationToken);

            return sd;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<List<SystemDetails>> GetHistoryByComputerIdAsync(int computerId, int limit = 10, CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_system_details_history] @computer_id, @limit";
            command.Parameters.Add(new SqlParameter("@computer_id", SqlDbType.Int) { Value = computerId });
            command.Parameters.Add(new SqlParameter("@limit", SqlDbType.Int) { Value = limit });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // RS1: system_details (no computer join)
            var items = new List<SystemDetails>();
            while (await reader.ReadAsync(cancellationToken))
                items.Add(MapSystemDetailsFromReader(reader));

            var map = items.ToDictionary(sd => sd.Id);

            // RS2: os
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var os = MapOsFromReader(reader);
                    if (map.TryGetValue(os.SystemDetailId, out var sd)) sd.Os = os;
                }
            }

            // RS3: performance
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var perf = MapPerformanceFromReader(reader);
                    if (map.TryGetValue(perf.SystemDetailId, out var sd)) sd.Performance = perf;
                }
            }

            // RS4: software
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var sw = MapSoftwareFromReader(reader);
                    if (map.TryGetValue(sw.SystemDetailId, out var sd)) sd.Software = sw;
                }
            }

            return items;
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<List<SystemDetails>> GetLatestForAllComputersAsync(CancellationToken cancellationToken = default)
    {
        var (items, _) = await ExecuteGetAllLatestAsync(null, null, cancellationToken);
        return items;
    }

    public async Task<(List<SystemDetails> Items, int TotalCount)> GetPagedLatestForAllComputersAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        return await ExecuteGetAllLatestAsync(page, pageSize, cancellationToken);
    }

    private async Task<(List<SystemDetails> Items, int TotalCount)> ExecuteGetAllLatestAsync(int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_latest_system_details_all] @page, @page_size";
            command.Parameters.Add(new SqlParameter("@page", SqlDbType.Int) { Value = (object?)page ?? DBNull.Value });
            command.Parameters.Add(new SqlParameter("@page_size", SqlDbType.Int) { Value = (object?)pageSize ?? DBNull.Value });

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // RS1: total count
            int totalCount = 0;
            if (await reader.ReadAsync(cancellationToken))
                totalCount = reader.GetInt32(0);

            // RS2: system_details + computer
            var items = new List<SystemDetails>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                    items.Add(MapSystemDetailsWithComputerFromReader(reader));
            }

            // RS3-RS18: All 16 sub-entities (multi-record, correlate by system_detail_id)
            var map = items.ToDictionary(sd => sd.Id);
            await ReadAllSubEntitiesMulti(reader, map, cancellationToken);

            return (items, totalCount);
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    public async Task<(List<SystemDetailsReport> Items, int TotalCount)> GetPagedSystemDetailsReportAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var connection = _context.Database.GetDbConnection();
        var wasOpen = connection.State == ConnectionState.Open;
        if (!wasOpen) await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "EXEC [sp_get_system_details_report] @page, @page_size";
            command.Parameters.Add(new SqlParameter("@page", page));
            command.Parameters.Add(new SqlParameter("@page_size", pageSize));

            if (_context.Database.CurrentTransaction is { } tx)
                command.Transaction = tx.GetDbTransaction();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            // Result set 1: total count
            int totalCount = 0;
            if (await reader.ReadAsync(cancellationToken))
                totalCount = reader.GetInt32(0);

            // Result set 2: paged report rows
            var items = new List<SystemDetailsReport>();
            if (await reader.NextResultAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                    items.Add(MapReportFromReader(reader));
            }

            return (items, totalCount);
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync();
        }
    }

    private static SystemDetailsReport MapReportFromReader(DbDataReader r) => new()
    {
        // Core
        Id = (int)r["id"],
        ComputerId = r["computer_id"] as int?,
        CollectedAt = (DateTime)r["collected_at"],
        MachineGuid = r["machine_guid"] as string,
        ProductId = r["product_id"] as string,
        ComputerName = r["hostname"] as string ?? "Unknown",
        SerialNumber = r["serial_number"] as string,

        // BIOS
        BiosVersion = r["bios_version"] as string,
        BiosManufacturer = r["bios_manufacturer"] as string,
        BiosReleaseDate = r["bios_release_date"] as DateTime?,
        BiosSerialNumber = r["bios_serial_number"] as string,
        SmbiosVersion = r["smbios_version"] as string,

        // Motherboard
        MotherboardManufacturer = r["motherboard_manufacturer"] as string,
        MotherboardProduct = r["motherboard_product"] as string,
        MotherboardSerialNumber = r["motherboard_serial_number"] as string,
        MotherboardVersion = r["motherboard_version"] as string,

        // CPU
        CpuName = r["cpu_name"] as string,
        CpuManufacturer = r["cpu_manufacturer"] as string,
        CpuCores = r["cpu_cores"] as int?,
        CpuLogicalProcessors = r["cpu_logical_processors"] as int?,
        CpuMaxClockSpeed = r["cpu_max_clock_speed"] as int?,
        CpuCurrentClockSpeed = r["cpu_current_clock_speed"] as int?,
        CpuArchitecture = r["cpu_architecture"] as string,
        CpuL2CacheSize = r["cpu_l2cache_size"] as int?,
        CpuL3CacheSize = r["cpu_l3cache_size"] as int?,
        CpuSocketDesignation = r["cpu_socket_designation"] as string,
        CpuProcessorId = r["cpu_processor_id"] as string,

        // Memory
        TotalPhysicalMemory = r["total_physical_memory"] as long?,
        AvailablePhysicalMemory = r["available_physical_memory"] as long?,
        TotalVirtualMemory = r["total_virtual_memory"] as long?,
        AvailableVirtualMemory = r["available_virtual_memory"] as long?,
        MemorySlots = r["memory_slots"] as int?,
        MemorySlotsUsed = r["memory_slots_used"] as int?,
        MemoryModulesJson = r["memory_modules_json"] as string,

        // Storage
        DisksJson = r["disks_json"] as string,
        PartitionsJson = r["partitions_json"] as string,
        LogicalDrivesJson = r["logical_drives_json"] as string,
        TotalDiskSpace = r["total_disk_space"] as long?,
        FreeDiskSpace = r["free_disk_space"] as long?,
        SharedFoldersJson = r["shared_folders_json"] as string,
        MappedDrivesJson = r["mapped_drives_json"] as string,

        // GPU
        GpuName = r["gpu_name"] as string,
        GpuManufacturer = r["gpu_manufacturer"] as string,
        GpuDriverVersion = r["gpu_driver_version"] as string,
        GpuDriverDate = r["gpu_driver_date"] as DateTime?,
        GpuRam = r["gpu_ram"] as long?,
        GpuResolution = r["gpu_resolution"] as string,
        GraphicsCardsJson = r["graphics_cards_json"] as string,
        MonitorsJson = r["monitors_json"] as string,
        MonitorCount = r["monitor_count"] as int?,

        // Network
        NetworkAdaptersJson = r["network_adapters_json"] as string,
        DomainName = r["domain_name"] as string,
        DnsHostname = r["dns_hostname"] as string,
        Workgroup = r["workgroup"] as string,
        DomainJoined = r["domain_joined"] as bool?,
        DefaultGateway = r["default_gateway"] as string,
        DnsServers = r["dns_servers"] as string,
        DhcpServer = r["dhcp_server"] as string,

        // OS
        OsName = r["os_name"] as string,
        OsVersion = r["os_version"] as string,
        OsBuild = r["os_build"] as string,
        OsArchitecture = r["os_architecture"] as string,
        OsInstallDate = r["os_install_date"] as string,
        OsLastBootTime = r["os_last_boot_time"] as string,
        OsSerialNumber = r["os_serial_number"] as string,
        OsProductKey = r["os_product_key"] as string,
        OsServicePackMajor = r["os_service_pack_major"] as int?,
        OsServicePackMinor = r["os_service_pack_minor"] as int?,
        OsLocale = r["os_locale"] as string,
        OsLanguage = r["os_language"] as string,
        TimeZone = r["time_zone"] as string,
        OsUptime = r["os_uptime"] as long?,
        WindowsProductId = r["windows_product_id"] as string,
        WindowsRegisteredOwner = r["windows_registered_owner"] as string,
        WindowsRegisteredOrganization = r["windows_registered_organization"] as string,
        WindowsIsActivated = r["windows_is_activated"] as bool?,
        WindowsActivationStatus = r["windows_activation_status"] as string,
        EnvironmentVariablesJson = r["environment_variables_json"] as string,

        // Security
        AntivirusProduct = r["antivirus_product"] as string,
        AntivirusStatus = r["antivirus_status"] as string,
        FirewallStatus = r["firewall_status"] as string,
        BitLockerEnabled = r["bit_locker_enabled"] as bool?,
        BitLockerStatus = r["bit_locker_status"] as string,
        SecureBootEnabled = r["secure_boot_enabled"] as bool?,
        TpmPresent = r["tpm_present"] as bool?,
        TpmVersion = r["tpm_version"] as string,
        UacEnabled = r["uac_enabled"] as bool?,

        // Battery
        HasBattery = r["has_battery"] as bool?,
        BatteryChargePercent = r["battery_charge_percent"] as int?,
        BatteryStatus = r["battery_status"] as string,
        BatteryEstimatedRuntime = r["battery_estimated_runtime"] as int?,
        BatteryDesignCapacity = r["battery_design_capacity"] as int?,
        BatteryFullChargeCapacity = r["battery_full_charge_capacity"] as int?,

        // Chassis
        SystemSkuNumber = r["system_sku_number"] as string,
        SystemFamily = r["system_family"] as string,
        ChassisType = r["chassis_type"] as string,
        ChassisManufacturer = r["chassis_manufacturer"] as string,
        ChassisSerialNumber = r["chassis_serial_number"] as string,
        ChassisAssetTag = r["chassis_asset_tag"] as string,

        // Software
        InstalledSoftwareJson = r["installed_software_json"] as string,
        InstalledSoftwareCount = r["installed_software_count"] as int?,
        InstalledUpdatesJson = r["installed_updates_json"] as string,
        InstalledUpdatesCount = r["installed_updates_count"] as int?,
        LastUpdateDate = r["last_update_date"] as DateTime?,
        RunningProcessesJson = r["running_processes_json"] as string,
        RunningProcessesCount = r["running_processes_count"] as int?,
        ServicesJson = r["services_json"] as string,
        RunningServicesCount = r["running_services_count"] as int?,
        StoppedServicesCount = r["stopped_services_count"] as int?,
        StartupProgramsJson = r["startup_programs_json"] as string,
        StartupProgramsCount = r["startup_programs_count"] as int?,
        ScheduledTasksJson = r["scheduled_tasks_json"] as string,
        ScheduledTasksCount = r["scheduled_tasks_count"] as int?,

        // Users / Groups
        LocalUsersJson = r["local_users_json"] as string,
        LoggedOnUsersJson = r["logged_on_users_json"] as string,

        // Peripherals
        PrintersJson = r["printers_json"] as string,
        UsbDevicesJson = r["usb_devices_json"] as string,
        AudioDevicesJson = r["audio_devices_json"] as string,

        // Event Logs
        RecentErrorsJson = r["recent_errors_json"] as string,
        RecentWarningsJson = r["recent_warnings_json"] as string,
        BlueScreenCount = r["blue_screen_count"] as int?,
        LastBlueScreen = r["last_blue_screen"] as DateTime?,

        // Performance
        CpuUsagePercent = r["cpu_usage_percent"] as double?,
        MemoryUsagePercent = r["memory_usage_percent"] as double?,
        DiskUsagePercent = r["disk_usage_percent"] as double?,
    };
}
