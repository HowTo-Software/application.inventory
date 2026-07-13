using System.Data.Common;
using ComputerInventory.Core.Entities;

namespace ComputerInventory.Infrastructure.Repositories;

/// <summary>
/// Mapper methods for reading SystemDetails and all 16 sub-entities from DbDataReader.
/// Used by SystemDetailsRepository for multi-result-set stored procedures.
/// </summary>
internal static class SystemDetailsMappers
{
    // ── SystemDetails mappers ───────────────────────────────────────────

    public static SystemDetails MapSystemDetailsFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        ComputerId = (int)r["computer_id"],
        CollectedAt = (DateTime)r["collected_at"],
        MachineGuid = r["machine_guid"] as string,
        ProductId = r["product_id"] as string,
    };

    public static SystemDetails MapSystemDetailsWithComputerFromReader(DbDataReader r)
    {
        var sd = MapSystemDetailsFromReader(r);
        sd.Computer = new Computer
        {
            Id = sd.ComputerId,
            Hostname = r["_computer_hostname"] as string ?? string.Empty,
            SerialNumber = r["_computer_serial_number"] as string ?? string.Empty,
        };
        return sd;
    }

    // ── Multi-result-set helpers ────────────────────────────────────────

    /// <summary>Reads 16 consecutive result sets (RS2-RS17) for a single SystemDetails record.</summary>
    public static async Task ReadAllSubEntitiesSingle(DbDataReader reader, SystemDetails sd, CancellationToken ct)
    {
        if (await reader.NextResultAsync(ct) && await reader.ReadAsync(ct)) sd.Bios = MapBiosFromReader(reader);
        if (await reader.NextResultAsync(ct) && await reader.ReadAsync(ct)) sd.Motherboard = MapMotherboardFromReader(reader);
        if (await reader.NextResultAsync(ct) && await reader.ReadAsync(ct)) sd.Cpu = MapCpuFromReader(reader);
        if (await reader.NextResultAsync(ct) && await reader.ReadAsync(ct)) sd.Memory = MapMemoryFromReader(reader);
        if (await reader.NextResultAsync(ct) && await reader.ReadAsync(ct)) sd.Storage = MapStorageFromReader(reader);
        if (await reader.NextResultAsync(ct) && await reader.ReadAsync(ct)) sd.Gpu = MapGpuFromReader(reader);
        if (await reader.NextResultAsync(ct) && await reader.ReadAsync(ct)) sd.Network = MapNetworkFromReader(reader);
        if (await reader.NextResultAsync(ct) && await reader.ReadAsync(ct)) sd.Os = MapOsFromReader(reader);
        if (await reader.NextResultAsync(ct) && await reader.ReadAsync(ct)) sd.Security = MapSecurityFromReader(reader);
        if (await reader.NextResultAsync(ct) && await reader.ReadAsync(ct)) sd.Battery = MapBatteryFromReader(reader);
        if (await reader.NextResultAsync(ct) && await reader.ReadAsync(ct)) sd.Chassis = MapChassisFromReader(reader);
        if (await reader.NextResultAsync(ct) && await reader.ReadAsync(ct)) sd.Software = MapSoftwareFromReader(reader);
        if (await reader.NextResultAsync(ct) && await reader.ReadAsync(ct)) sd.UsersGroups = MapUsersGroupsFromReader(reader);
        if (await reader.NextResultAsync(ct) && await reader.ReadAsync(ct)) sd.Peripherals = MapPeripheralsFromReader(reader);
        if (await reader.NextResultAsync(ct) && await reader.ReadAsync(ct)) sd.EventLogs = MapEventLogsFromReader(reader);
        if (await reader.NextResultAsync(ct) && await reader.ReadAsync(ct)) sd.Performance = MapPerformanceFromReader(reader);
    }

    /// <summary>Reads 16 consecutive result sets (RS3-RS18) for multiple SystemDetails records, correlating by system_detail_id.</summary>
    public static async Task ReadAllSubEntitiesMulti(DbDataReader reader, Dictionary<int, SystemDetails> map, CancellationToken ct)
    {
        await ReadAndAttach(reader, map, MapBiosFromReader, static (sd, e) => sd.Bios = e, static e => e.SystemDetailId, ct);
        await ReadAndAttach(reader, map, MapMotherboardFromReader, static (sd, e) => sd.Motherboard = e, static e => e.SystemDetailId, ct);
        await ReadAndAttach(reader, map, MapCpuFromReader, static (sd, e) => sd.Cpu = e, static e => e.SystemDetailId, ct);
        await ReadAndAttach(reader, map, MapMemoryFromReader, static (sd, e) => sd.Memory = e, static e => e.SystemDetailId, ct);
        await ReadAndAttach(reader, map, MapStorageFromReader, static (sd, e) => sd.Storage = e, static e => e.SystemDetailId, ct);
        await ReadAndAttach(reader, map, MapGpuFromReader, static (sd, e) => sd.Gpu = e, static e => e.SystemDetailId, ct);
        await ReadAndAttach(reader, map, MapNetworkFromReader, static (sd, e) => sd.Network = e, static e => e.SystemDetailId, ct);
        await ReadAndAttach(reader, map, MapOsFromReader, static (sd, e) => sd.Os = e, static e => e.SystemDetailId, ct);
        await ReadAndAttach(reader, map, MapSecurityFromReader, static (sd, e) => sd.Security = e, static e => e.SystemDetailId, ct);
        await ReadAndAttach(reader, map, MapBatteryFromReader, static (sd, e) => sd.Battery = e, static e => e.SystemDetailId, ct);
        await ReadAndAttach(reader, map, MapChassisFromReader, static (sd, e) => sd.Chassis = e, static e => e.SystemDetailId, ct);
        await ReadAndAttach(reader, map, MapSoftwareFromReader, static (sd, e) => sd.Software = e, static e => e.SystemDetailId, ct);
        await ReadAndAttach(reader, map, MapUsersGroupsFromReader, static (sd, e) => sd.UsersGroups = e, static e => e.SystemDetailId, ct);
        await ReadAndAttach(reader, map, MapPeripheralsFromReader, static (sd, e) => sd.Peripherals = e, static e => e.SystemDetailId, ct);
        await ReadAndAttach(reader, map, MapEventLogsFromReader, static (sd, e) => sd.EventLogs = e, static e => e.SystemDetailId, ct);
        await ReadAndAttach(reader, map, MapPerformanceFromReader, static (sd, e) => sd.Performance = e, static e => e.SystemDetailId, ct);
    }

    private static async Task ReadAndAttach<T>(
        DbDataReader reader,
        Dictionary<int, SystemDetails> map,
        Func<DbDataReader, T> mapper,
        Action<SystemDetails, T> setter,
        Func<T, int> getKey,
        CancellationToken ct)
    {
        if (!await reader.NextResultAsync(ct)) return;
        while (await reader.ReadAsync(ct))
        {
            var entity = mapper(reader);
            if (map.TryGetValue(getKey(entity), out var sd))
                setter(sd, entity);
        }
    }

    // ── Sub-entity mappers ──────────────────────────────────────────────

    public static SystemDetailBios MapBiosFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        SystemDetailId = (int)r["system_detail_id"],
        BiosVersion = r["bios_version"] as string,
        BiosManufacturer = r["bios_manufacturer"] as string,
        BiosReleaseDate = r["bios_release_date"] is DBNull ? null : (DateTime)r["bios_release_date"],
        BiosSerialNumber = r["bios_serial_number"] as string,
        SmbiosVersion = r["smbios_version"] as string,
    };

    public static SystemDetailMotherboard MapMotherboardFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        SystemDetailId = (int)r["system_detail_id"],
        MotherboardManufacturer = r["motherboard_manufacturer"] as string,
        MotherboardProduct = r["motherboard_product"] as string,
        MotherboardSerialNumber = r["motherboard_serial_number"] as string,
        MotherboardVersion = r["motherboard_version"] as string,
    };

    public static SystemDetailCpu MapCpuFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        SystemDetailId = (int)r["system_detail_id"],
        CpuName = r["cpu_name"] as string,
        CpuManufacturer = r["cpu_manufacturer"] as string,
        CpuCores = r["cpu_cores"] is DBNull ? null : (int)r["cpu_cores"],
        CpuLogicalProcessors = r["cpu_logical_processors"] is DBNull ? null : (int)r["cpu_logical_processors"],
        CpuMaxClockSpeed = r["cpu_max_clock_speed"] is DBNull ? null : (int)r["cpu_max_clock_speed"],
        CpuCurrentClockSpeed = r["cpu_current_clock_speed"] is DBNull ? null : (int)r["cpu_current_clock_speed"],
        CpuArchitecture = r["cpu_architecture"] as string,
        CpuL2CacheSize = r["cpu_l2cache_size"] is DBNull ? null : (int)r["cpu_l2cache_size"],
        CpuL3CacheSize = r["cpu_l3cache_size"] is DBNull ? null : (int)r["cpu_l3cache_size"],
        CpuSocketDesignation = r["cpu_socket_designation"] as string,
        CpuProcessorId = r["cpu_processor_id"] as string,
    };

    public static SystemDetailMemory MapMemoryFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        SystemDetailId = (int)r["system_detail_id"],
        TotalPhysicalMemory = r["total_physical_memory"] is DBNull ? null : (long)r["total_physical_memory"],
        AvailablePhysicalMemory = r["available_physical_memory"] is DBNull ? null : (long)r["available_physical_memory"],
        TotalVirtualMemory = r["total_virtual_memory"] is DBNull ? null : (long)r["total_virtual_memory"],
        AvailableVirtualMemory = r["available_virtual_memory"] is DBNull ? null : (long)r["available_virtual_memory"],
        MemorySlots = r["memory_slots"] is DBNull ? null : (int)r["memory_slots"],
        MemorySlotsUsed = r["memory_slots_used"] is DBNull ? null : (int)r["memory_slots_used"],
        MemoryModulesJson = r["memory_modules_json"] as string,
    };

    public static SystemDetailStorage MapStorageFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        SystemDetailId = (int)r["system_detail_id"],
        DisksJson = r["disks_json"] as string,
        PartitionsJson = r["partitions_json"] as string,
        LogicalDrivesJson = r["logical_drives_json"] as string,
        TotalDiskSpace = r["total_disk_space"] is DBNull ? null : (long)r["total_disk_space"],
        FreeDiskSpace = r["free_disk_space"] is DBNull ? null : (long)r["free_disk_space"],
        SharedFoldersJson = r["shared_folders_json"] as string,
        MappedDrivesJson = r["mapped_drives_json"] as string,
    };

    public static SystemDetailGpu MapGpuFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        SystemDetailId = (int)r["system_detail_id"],
        GpuName = r["gpu_name"] as string,
        GpuManufacturer = r["gpu_manufacturer"] as string,
        GpuDriverVersion = r["gpu_driver_version"] as string,
        GpuDriverDate = r["gpu_driver_date"] is DBNull ? null : (DateTime)r["gpu_driver_date"],
        GpuRam = r["gpu_ram"] is DBNull ? null : (long)r["gpu_ram"],
        GpuResolution = r["gpu_resolution"] as string,
        GraphicsCardsJson = r["graphics_cards_json"] as string,
        MonitorsJson = r["monitors_json"] as string,
        MonitorCount = r["monitor_count"] is DBNull ? null : (int)r["monitor_count"],
    };

    public static SystemDetailNetwork MapNetworkFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        SystemDetailId = (int)r["system_detail_id"],
        NetworkAdaptersJson = r["network_adapters_json"] as string,
        DomainName = r["domain_name"] as string,
        DnsHostname = r["dns_hostname"] as string,
        Workgroup = r["workgroup"] as string,
        DomainJoined = r["domain_joined"] is DBNull ? null : (bool)r["domain_joined"],
        DefaultGateway = r["default_gateway"] as string,
        DnsServers = r["dns_servers"] as string,
        DhcpServer = r["dhcp_server"] as string,
    };

    public static SystemDetailOs MapOsFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        SystemDetailId = (int)r["system_detail_id"],
        OsName = r["os_name"] as string,
        OsVersion = r["os_version"] as string,
        OsBuild = r["os_build"] as string,
        OsArchitecture = r["os_architecture"] as string,
        OsInstallDate = r["os_install_date"] as string,
        OsLastBootTime = r["os_last_boot_time"] as string,
        OsSerialNumber = r["os_serial_number"] as string,
        OsProductKey = r["os_product_key"] as string,
        OsServicePackMajor = r["os_service_pack_major"] is DBNull ? null : (int)r["os_service_pack_major"],
        OsServicePackMinor = r["os_service_pack_minor"] is DBNull ? null : (int)r["os_service_pack_minor"],
        OsLocale = r["os_locale"] as string,
        OsLanguage = r["os_language"] as string,
        TimeZone = r["time_zone"] as string,
        OsUptime = r["os_uptime"] is DBNull ? null : (long)r["os_uptime"],
        WindowsProductId = r["windows_product_id"] as string,
        WindowsRegisteredOwner = r["windows_registered_owner"] as string,
        WindowsRegisteredOrganization = r["windows_registered_organization"] as string,
        WindowsIsActivated = r["windows_is_activated"] is DBNull ? null : (bool)r["windows_is_activated"],
        WindowsActivationStatus = r["windows_activation_status"] as string,
        EnvironmentVariablesJson = r["environment_variables_json"] as string,
    };

    public static SystemDetailSecurity MapSecurityFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        SystemDetailId = (int)r["system_detail_id"],
        AntivirusProduct = r["antivirus_product"] as string,
        AntivirusStatus = r["antivirus_status"] as string,
        FirewallStatus = r["firewall_status"] as string,
        BitLockerEnabled = r["bit_locker_enabled"] is DBNull ? null : (bool)r["bit_locker_enabled"],
        BitLockerStatus = r["bit_locker_status"] as string,
        SecureBootEnabled = r["secure_boot_enabled"] is DBNull ? null : (bool)r["secure_boot_enabled"],
        TpmPresent = r["tpm_present"] is DBNull ? null : (bool)r["tpm_present"],
        TpmVersion = r["tpm_version"] as string,
        UacEnabled = r["uac_enabled"] is DBNull ? null : (bool)r["uac_enabled"],
    };

    public static SystemDetailBattery MapBatteryFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        SystemDetailId = (int)r["system_detail_id"],
        HasBattery = r["has_battery"] is DBNull ? null : (bool)r["has_battery"],
        BatteryChargePercent = r["battery_charge_percent"] is DBNull ? null : (int)r["battery_charge_percent"],
        BatteryStatus = r["battery_status"] as string,
        BatteryEstimatedRuntime = r["battery_estimated_runtime"] is DBNull ? null : (int)r["battery_estimated_runtime"],
        BatteryHealth = r["battery_health"] as string,
        BatteryCycleCount = r["battery_cycle_count"] is DBNull ? null : (int)r["battery_cycle_count"],
        BatteryDesignCapacity = r["battery_design_capacity"] is DBNull ? null : (int)r["battery_design_capacity"],
        BatteryFullChargeCapacity = r["battery_full_charge_capacity"] is DBNull ? null : (int)r["battery_full_charge_capacity"],
    };

    public static SystemDetailChassis MapChassisFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        SystemDetailId = (int)r["system_detail_id"],
        SystemSkuNumber = r["system_sku_number"] as string,
        SystemFamily = r["system_family"] as string,
        ChassisType = r["chassis_type"] as string,
        ChassisManufacturer = r["chassis_manufacturer"] as string,
        ChassisSerialNumber = r["chassis_serial_number"] as string,
        ChassisAssetTag = r["chassis_asset_tag"] as string,
    };

    public static SystemDetailSoftware MapSoftwareFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        SystemDetailId = (int)r["system_detail_id"],
        InstalledSoftwareJson = r["installed_software_json"] as string,
        InstalledSoftwareCount = r["installed_software_count"] is DBNull ? null : (int)r["installed_software_count"],
        InstalledUpdatesJson = r["installed_updates_json"] as string,
        InstalledUpdatesCount = r["installed_updates_count"] is DBNull ? null : (int)r["installed_updates_count"],
        LastUpdateDate = r["last_update_date"] is DBNull ? null : (DateTime)r["last_update_date"],
        RunningProcessesJson = r["running_processes_json"] as string,
        RunningProcessesCount = r["running_processes_count"] is DBNull ? null : (int)r["running_processes_count"],
        ServicesJson = r["services_json"] as string,
        RunningServicesCount = r["running_services_count"] is DBNull ? null : (int)r["running_services_count"],
        StoppedServicesCount = r["stopped_services_count"] is DBNull ? null : (int)r["stopped_services_count"],
        StartupProgramsJson = r["startup_programs_json"] as string,
        StartupProgramsCount = r["startup_programs_count"] is DBNull ? null : (int)r["startup_programs_count"],
        ScheduledTasksJson = r["scheduled_tasks_json"] as string,
        ScheduledTasksCount = r["scheduled_tasks_count"] is DBNull ? null : (int)r["scheduled_tasks_count"],
    };

    public static SystemDetailUsersGroups MapUsersGroupsFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        SystemDetailId = (int)r["system_detail_id"],
        LocalUsersJson = r["local_users_json"] as string,
        LocalGroupsJson = r["local_groups_json"] as string,
        LoggedOnUsersJson = r["logged_on_users_json"] as string,
    };

    public static SystemDetailPeripherals MapPeripheralsFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        SystemDetailId = (int)r["system_detail_id"],
        PrintersJson = r["printers_json"] as string,
        UsbDevicesJson = r["usb_devices_json"] as string,
        AudioDevicesJson = r["audio_devices_json"] as string,
    };

    public static SystemDetailEventLogs MapEventLogsFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        SystemDetailId = (int)r["system_detail_id"],
        RecentErrorsJson = r["recent_errors_json"] as string,
        RecentWarningsJson = r["recent_warnings_json"] as string,
        BlueScreenCount = r["blue_screen_count"] is DBNull ? null : (int)r["blue_screen_count"],
        LastBlueScreen = r["last_blue_screen"] is DBNull ? null : (DateTime)r["last_blue_screen"],
    };

    public static SystemDetailPerformance MapPerformanceFromReader(DbDataReader r) => new()
    {
        Id = (int)r["id"],
        SystemDetailId = (int)r["system_detail_id"],
        CpuUsagePercent = r["cpu_usage_percent"] is DBNull ? null : (double)r["cpu_usage_percent"],
        MemoryUsagePercent = r["memory_usage_percent"] is DBNull ? null : (double)r["memory_usage_percent"],
        DiskUsagePercent = r["disk_usage_percent"] is DBNull ? null : (double)r["disk_usage_percent"],
    };
}
