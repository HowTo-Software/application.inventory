using System.ComponentModel.DataAnnotations.Schema;

namespace ComputerInventory.Core.Entities;

// ============================================================================
// SYSTEM_DETAIL Sub-Entities (3NF decomposition)
// Each sub-table holds attributes for a single hardware/software concern.
// All have a 1:1 relationship with SYSTEM_DETAILS via SystemDetailId.
// ============================================================================

public class SystemDetailBios
{
    public int Id { get; set; }
    public int SystemDetailId { get; set; }
    public string? BiosVersion { get; set; }
    public string? BiosManufacturer { get; set; }
    public DateTime? BiosReleaseDate { get; set; }
    public string? BiosSerialNumber { get; set; }
    public string? SmbiosVersion { get; set; }
    [InverseProperty(nameof(SystemDetails.Bios))]
    public SystemDetails SystemDetail { get; set; } = null!;
}

public class SystemDetailMotherboard
{
    public int Id { get; set; }
    public int SystemDetailId { get; set; }
    public string? MotherboardManufacturer { get; set; }
    public string? MotherboardProduct { get; set; }
    public string? MotherboardSerialNumber { get; set; }
    public string? MotherboardVersion { get; set; }
    [InverseProperty(nameof(SystemDetails.Motherboard))]
    public SystemDetails SystemDetail { get; set; } = null!;
}

public class SystemDetailCpu
{
    public int Id { get; set; }
    public int SystemDetailId { get; set; }
    public string? CpuName { get; set; }
    public string? CpuManufacturer { get; set; }
    public int? CpuCores { get; set; }
    public int? CpuLogicalProcessors { get; set; }
    public int? CpuMaxClockSpeed { get; set; }
    public int? CpuCurrentClockSpeed { get; set; }
    public string? CpuArchitecture { get; set; }
    public int? CpuL2CacheSize { get; set; }
    public int? CpuL3CacheSize { get; set; }
    public string? CpuSocketDesignation { get; set; }
    public string? CpuProcessorId { get; set; }
    [InverseProperty(nameof(SystemDetails.Cpu))]
    public SystemDetails SystemDetail { get; set; } = null!;
}

public class SystemDetailMemory
{
    public int Id { get; set; }
    public int SystemDetailId { get; set; }
    public long? TotalPhysicalMemory { get; set; }
    public long? AvailablePhysicalMemory { get; set; }
    public long? TotalVirtualMemory { get; set; }
    public long? AvailableVirtualMemory { get; set; }
    public int? MemorySlots { get; set; }
    public int? MemorySlotsUsed { get; set; }
    public string? MemoryModulesJson { get; set; }
    [InverseProperty(nameof(SystemDetails.Memory))]
    public SystemDetails SystemDetail { get; set; } = null!;
}

public class SystemDetailStorage
{
    public int Id { get; set; }
    public int SystemDetailId { get; set; }
    public string? DisksJson { get; set; }
    public string? PartitionsJson { get; set; }
    public string? LogicalDrivesJson { get; set; }
    public long? TotalDiskSpace { get; set; }
    public long? FreeDiskSpace { get; set; }
    public string? SharedFoldersJson { get; set; }
    public string? MappedDrivesJson { get; set; }
    [InverseProperty(nameof(SystemDetails.Storage))]
    public SystemDetails SystemDetail { get; set; } = null!;
}

public class SystemDetailGpu
{
    public int Id { get; set; }
    public int SystemDetailId { get; set; }
    public string? GpuName { get; set; }
    public string? GpuManufacturer { get; set; }
    public string? GpuDriverVersion { get; set; }
    public DateTime? GpuDriverDate { get; set; }
    public long? GpuRam { get; set; }
    public string? GpuResolution { get; set; }
    public string? GraphicsCardsJson { get; set; }
    public string? MonitorsJson { get; set; }
    public int? MonitorCount { get; set; }
    [InverseProperty(nameof(SystemDetails.Gpu))]
    public SystemDetails SystemDetail { get; set; } = null!;
}

public class SystemDetailNetwork
{
    public int Id { get; set; }
    public int SystemDetailId { get; set; }
    public string? NetworkAdaptersJson { get; set; }
    public string? DomainName { get; set; }
    public string? DnsHostname { get; set; }
    public string? Workgroup { get; set; }
    public bool? DomainJoined { get; set; }
    public string? DefaultGateway { get; set; }
    public string? DnsServers { get; set; }
    public string? DhcpServer { get; set; }
    [InverseProperty(nameof(SystemDetails.Network))]
    public SystemDetails SystemDetail { get; set; } = null!;
}

public class SystemDetailOs
{
    public int Id { get; set; }
    public int SystemDetailId { get; set; }
    public string? OsName { get; set; }
    public string? OsVersion { get; set; }
    public string? OsBuild { get; set; }
    public string? OsArchitecture { get; set; }
    public string? OsInstallDate { get; set; }
    public string? OsLastBootTime { get; set; }
    public string? OsSerialNumber { get; set; }
    public string? OsProductKey { get; set; }
    public int? OsServicePackMajor { get; set; }
    public int? OsServicePackMinor { get; set; }
    public string? OsLocale { get; set; }
    public string? OsLanguage { get; set; }
    public string? TimeZone { get; set; }
    public long? OsUptime { get; set; }
    public string? WindowsProductId { get; set; }
    public string? WindowsRegisteredOwner { get; set; }
    public string? WindowsRegisteredOrganization { get; set; }
    public bool? WindowsIsActivated { get; set; }
    public string? WindowsActivationStatus { get; set; }
    public string? EnvironmentVariablesJson { get; set; }
    [InverseProperty(nameof(SystemDetails.Os))]
    public SystemDetails SystemDetail { get; set; } = null!;
}

public class SystemDetailSecurity
{
    public int Id { get; set; }
    public int SystemDetailId { get; set; }
    public string? AntivirusProduct { get; set; }
    public string? AntivirusStatus { get; set; }
    public string? FirewallStatus { get; set; }
    public bool? BitLockerEnabled { get; set; }
    public string? BitLockerStatus { get; set; }
    public bool? SecureBootEnabled { get; set; }
    public bool? TpmPresent { get; set; }
    public string? TpmVersion { get; set; }
    public bool? UacEnabled { get; set; }
    [InverseProperty(nameof(SystemDetails.Security))]
    public SystemDetails SystemDetail { get; set; } = null!;
}

public class SystemDetailBattery
{
    public int Id { get; set; }
    public int SystemDetailId { get; set; }
    public bool? HasBattery { get; set; }
    public int? BatteryChargePercent { get; set; }
    public string? BatteryStatus { get; set; }
    public int? BatteryEstimatedRuntime { get; set; }
    public string? BatteryHealth { get; set; }
    public int? BatteryCycleCount { get; set; }
    public int? BatteryDesignCapacity { get; set; }
    public int? BatteryFullChargeCapacity { get; set; }
    [InverseProperty(nameof(SystemDetails.Battery))]
    public SystemDetails SystemDetail { get; set; } = null!;
}

public class SystemDetailChassis
{
    public int Id { get; set; }
    public int SystemDetailId { get; set; }
    public string? SystemSkuNumber { get; set; }
    public string? SystemFamily { get; set; }
    public string? ChassisType { get; set; }
    public string? ChassisManufacturer { get; set; }
    public string? ChassisSerialNumber { get; set; }
    public string? ChassisAssetTag { get; set; }
    [InverseProperty(nameof(SystemDetails.Chassis))]
    public SystemDetails SystemDetail { get; set; } = null!;
}

public class SystemDetailSoftware
{
    public int Id { get; set; }
    public int SystemDetailId { get; set; }
    public string? InstalledSoftwareJson { get; set; }
    public int? InstalledSoftwareCount { get; set; }
    public string? InstalledUpdatesJson { get; set; }
    public int? InstalledUpdatesCount { get; set; }
    public DateTime? LastUpdateDate { get; set; }
    public string? RunningProcessesJson { get; set; }
    public int? RunningProcessesCount { get; set; }
    public string? ServicesJson { get; set; }
    public int? RunningServicesCount { get; set; }
    public int? StoppedServicesCount { get; set; }
    public string? StartupProgramsJson { get; set; }
    public int? StartupProgramsCount { get; set; }
    public string? ScheduledTasksJson { get; set; }
    public int? ScheduledTasksCount { get; set; }
    [InverseProperty(nameof(SystemDetails.Software))]
    public SystemDetails SystemDetail { get; set; } = null!;
}

public class SystemDetailUsersGroups
{
    public int Id { get; set; }
    public int SystemDetailId { get; set; }
    public string? LocalUsersJson { get; set; }
    public string? LocalGroupsJson { get; set; }
    public string? LoggedOnUsersJson { get; set; }
    [InverseProperty(nameof(SystemDetails.UsersGroups))]
    public SystemDetails SystemDetail { get; set; } = null!;
}

public class SystemDetailPeripherals
{
    public int Id { get; set; }
    public int SystemDetailId { get; set; }
    public string? PrintersJson { get; set; }
    public string? UsbDevicesJson { get; set; }
    public string? AudioDevicesJson { get; set; }
    [InverseProperty(nameof(SystemDetails.Peripherals))]
    public SystemDetails SystemDetail { get; set; } = null!;
}

public class SystemDetailEventLogs
{
    public int Id { get; set; }
    public int SystemDetailId { get; set; }
    public string? RecentErrorsJson { get; set; }
    public string? RecentWarningsJson { get; set; }
    public int? BlueScreenCount { get; set; }
    public DateTime? LastBlueScreen { get; set; }
    [InverseProperty(nameof(SystemDetails.EventLogs))]
    public SystemDetails SystemDetail { get; set; } = null!;
}

public class SystemDetailPerformance
{
    public int Id { get; set; }
    public int SystemDetailId { get; set; }
    public double? CpuUsagePercent { get; set; }
    public double? MemoryUsagePercent { get; set; }
    public double? DiskUsagePercent { get; set; }
    [InverseProperty(nameof(SystemDetails.Performance))]
    public SystemDetails SystemDetail { get; set; } = null!;
}
