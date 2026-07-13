namespace ComputerInventory.Contracts;

public class SystemDetailsReport
{
    public int Id { get; set; }
    public int? ComputerId { get; set; }
    public string? ComputerName { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime CollectedAt { get; set; }

    // BIOS
    public string? BiosVersion { get; set; }
    public string? BiosManufacturer { get; set; }
    public string? BiosSerialNumber { get; set; }
    public string? SmbiosVersion { get; set; }
    public DateTime? BiosReleaseDate { get; set; }

    // Motherboard
    public string? MotherboardManufacturer { get; set; }
    public string? MotherboardProduct { get; set; }
    public string? MotherboardSerialNumber { get; set; }
    public string? MotherboardVersion { get; set; }

    // CPU
    public string? CpuName { get; set; }
    public string? CpuManufacturer { get; set; }
    public int? CpuCores { get; set; }
    public int? CpuLogicalProcessors { get; set; }
    public int? CpuMaxClockSpeed { get; set; }
    public int? CpuCurrentClockSpeed { get; set; }
    public int? CpuL2CacheSize { get; set; }
    public int? CpuL3CacheSize { get; set; }
    public string? CpuSocketDesignation { get; set; }
    public string? CpuArchitecture { get; set; }
    public string? CpuProcessorId { get; set; }

    // Memory
    public long? TotalPhysicalMemory { get; set; }
    public long? AvailablePhysicalMemory { get; set; }
    public long? TotalVirtualMemory { get; set; }
    public long? AvailableVirtualMemory { get; set; }
    public int? MemorySlots { get; set; }
    public int? MemorySlotsUsed { get; set; }
    public string? MemoryModulesJson { get; set; }

    // Storage
    public string? DisksJson { get; set; }
    public string? PartitionsJson { get; set; }
    public string? LogicalDrivesJson { get; set; }
    public long? TotalDiskSpace { get; set; }
    public long? FreeDiskSpace { get; set; }

    // GPU
    public string? GpuName { get; set; }
    public string? GpuManufacturer { get; set; }
    public string? GpuDriverVersion { get; set; }
    public DateTime? GpuDriverDate { get; set; }
    public long? GpuRam { get; set; }
    public string? GpuResolution { get; set; }
    public string? GraphicsCardsJson { get; set; }

    // Monitors
    public int? MonitorCount { get; set; }
    public string? MonitorsJson { get; set; }

    // Network
    public string? NetworkAdaptersJson { get; set; }
    public string? DefaultGateway { get; set; }
    public string? DnsServers { get; set; }
    public string? DhcpServer { get; set; }
    public string? DomainName { get; set; }
    public string? DnsHostname { get; set; }
    public string? Workgroup { get; set; }
    public bool? DomainJoined { get; set; }

    // OS
    public string? OsName { get; set; }
    public string? OsVersion { get; set; }
    public string? OsBuild { get; set; }
    public string? OsArchitecture { get; set; }
    public string? OsSerialNumber { get; set; }
    public int? OsServicePackMajor { get; set; }
    public int? OsServicePackMinor { get; set; }
    public string? OsLocale { get; set; }
    public string? OsLanguage { get; set; }
    public string? OsInstallDate { get; set; }
    public string? OsLastBootTime { get; set; }
    public long? OsUptime { get; set; }
    public string? TimeZone { get; set; }

    // License
    public string? WindowsProductId { get; set; }
    public string? OsProductKey { get; set; }
    public string? WindowsRegisteredOwner { get; set; }
    public string? WindowsRegisteredOrganization { get; set; }
    public bool? WindowsIsActivated { get; set; }
    public string? WindowsActivationStatus { get; set; }

    // Software
    public string? InstalledSoftwareJson { get; set; }
    public int? InstalledSoftwareCount { get; set; }

    // Updates
    public string? InstalledUpdatesJson { get; set; }
    public int? InstalledUpdatesCount { get; set; }
    public DateTime? LastUpdateDate { get; set; }

    // Processes
    public string? RunningProcessesJson { get; set; }
    public int? RunningProcessesCount { get; set; }

    // Services
    public string? ServicesJson { get; set; }
    public int? RunningServicesCount { get; set; }
    public int? StoppedServicesCount { get; set; }

    // Startup
    public string? StartupProgramsJson { get; set; }
    public int? StartupProgramsCount { get; set; }

    // Users
    public string? LocalUsersJson { get; set; }
    public string? LoggedOnUsersJson { get; set; }

    // Security
    public string? AntivirusProduct { get; set; }
    public string? AntivirusStatus { get; set; }
    public string? FirewallStatus { get; set; }
    public bool? TpmPresent { get; set; }
    public string? TpmVersion { get; set; }
    public bool? SecureBootEnabled { get; set; }
    public bool? UacEnabled { get; set; }
    public bool? BitLockerEnabled { get; set; }
    public string? BitLockerStatus { get; set; }

    // Peripherals
    public string? PrintersJson { get; set; }
    public string? UsbDevicesJson { get; set; }
    public string? AudioDevicesJson { get; set; }

    // Battery
    public bool? HasBattery { get; set; }
    public int? BatteryChargePercent { get; set; }
    public string? BatteryStatus { get; set; }
    public int? BatteryEstimatedRuntime { get; set; }
    public int? BatteryDesignCapacity { get; set; }
    public int? BatteryFullChargeCapacity { get; set; }

    // Events
    public string? RecentErrorsJson { get; set; }
    public string? RecentWarningsJson { get; set; }
    public int? BlueScreenCount { get; set; }
    public DateTime? LastBlueScreen { get; set; }

    // Performance
    public double? CpuUsagePercent { get; set; }
    public double? MemoryUsagePercent { get; set; }
    public double? DiskUsagePercent { get; set; }

    // Chassis
    public string? ChassisManufacturer { get; set; }
    public string? ChassisSerialNumber { get; set; }
    public string? ChassisType { get; set; }
    public string? ChassisAssetTag { get; set; }

    // Environment
    public string? EnvironmentVariablesJson { get; set; }

    // Tasks
    public string? ScheduledTasksJson { get; set; }
    public int? ScheduledTasksCount { get; set; }

    // Shares
    public string? SharedFoldersJson { get; set; }
    public string? MappedDrivesJson { get; set; }

    // Hardware IDs
    public string? MachineGuid { get; set; }
    public string? ProductId { get; set; }
    public string? SystemSkuNumber { get; set; }
    public string? SystemFamily { get; set; }
}
