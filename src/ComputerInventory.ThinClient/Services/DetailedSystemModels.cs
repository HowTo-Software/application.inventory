namespace ComputerInventory.ThinClient.Services;

/// <summary>
/// Comprehensive system information - EVERYTHING from Windows!
/// Properties grouped into logical sub-models.
/// </summary>
public class DetailedSystemInfo
{
    public BiosInfo Bios { get; set; } = new();
    public MotherboardInfo Motherboard { get; set; } = new();
    public CpuInfo Cpu { get; set; } = new();
    public MemoryInfo Memory { get; set; } = new();
    public StorageInfo Storage { get; set; } = new();
    public GpuInfo Gpu { get; set; } = new();
    public NetworkInfo Network { get; set; } = new();
    public OsInfo Os { get; set; } = new();
    public SecurityInfo Security { get; set; } = new();
    public BatteryInfo Battery { get; set; } = new();
    public ChassisInfo Chassis { get; set; } = new();
    public SoftwareInfo Software { get; set; } = new();
    public UsersGroupsInfo UsersGroups { get; set; } = new();
    public PeripheralsInfo Peripherals { get; set; } = new();
    public EventLogsInfo EventLogs { get; set; } = new();
    public PerformanceInfo Performance { get; set; } = new();
    public HardwareIdsInfo HardwareIds { get; set; } = new();
}

public class BiosInfo
{
    public string? BiosVersion { get; set; }
    public string? BiosManufacturer { get; set; }
    public string? BiosSerialNumber { get; set; }
    public string? SmbiosVersion { get; set; }
    public DateTime? BiosReleaseDate { get; set; }
}

public class MotherboardInfo
{
    public string? MotherboardManufacturer { get; set; }
    public string? MotherboardProduct { get; set; }
    public string? MotherboardSerialNumber { get; set; }
    public string? MotherboardVersion { get; set; }
}

public class CpuInfo
{
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
}

public class MemoryInfo
{
    public long? TotalPhysicalMemory { get; set; }
    public long? AvailablePhysicalMemory { get; set; }
    public long? TotalVirtualMemory { get; set; }
    public long? AvailableVirtualMemory { get; set; }
    public int? MemorySlots { get; set; }
    public int? MemorySlotsUsed { get; set; }
    public string? MemoryModulesJson { get; set; }
}

public class StorageInfo
{
    public string? DisksJson { get; set; }
    public string? PartitionsJson { get; set; }
    public string? LogicalDrivesJson { get; set; }
    public long? TotalDiskSpace { get; set; }
    public long? FreeDiskSpace { get; set; }
    public string? SharedFoldersJson { get; set; }
    public string? MappedDrivesJson { get; set; }
}

public class GpuInfo
{
    public string? GpuName { get; set; }
    public string? GpuManufacturer { get; set; }
    public string? GpuDriverVersion { get; set; }
    public DateTime? GpuDriverDate { get; set; }
    public long? GpuRam { get; set; }
    public string? GpuResolution { get; set; }
    public string? GraphicsCardsJson { get; set; }
    public int? MonitorCount { get; set; }
    public string? MonitorsJson { get; set; }
}

public class NetworkInfo
{
    public string? NetworkAdaptersJson { get; set; }
    public string? DefaultGateway { get; set; }
    public string? DnsServers { get; set; }
    public string? DhcpServer { get; set; }
    public string? DomainName { get; set; }
    public string? DnsHostname { get; set; }
    public string? Workgroup { get; set; }
    public bool? DomainJoined { get; set; }
}

public class OsInfo
{
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
    public string? WindowsProductId { get; set; }
    public string? OsProductKey { get; set; }
    public string? WindowsRegisteredOwner { get; set; }
    public string? WindowsRegisteredOrganization { get; set; }
    public bool? WindowsIsActivated { get; set; }
    public string? WindowsActivationStatus { get; set; }
    public string? EnvironmentVariablesJson { get; set; }
}

public class SecurityInfo
{
    public string? AntivirusProduct { get; set; }
    public string? AntivirusStatus { get; set; }
    public string? FirewallStatus { get; set; }
    public bool? TpmPresent { get; set; }
    public string? TpmVersion { get; set; }
    public bool? SecureBootEnabled { get; set; }
    public bool? UacEnabled { get; set; }
    public bool? BitLockerEnabled { get; set; }
    public string? BitLockerStatus { get; set; }
}

public class BatteryInfo
{
    public bool? HasBattery { get; set; }
    public int? BatteryChargePercent { get; set; }
    public string? BatteryStatus { get; set; }
    public int? BatteryEstimatedRuntime { get; set; }
    public int? BatteryDesignCapacity { get; set; }
    public int? BatteryFullChargeCapacity { get; set; }
}

public class ChassisInfo
{
    public string? ChassisManufacturer { get; set; }
    public string? ChassisSerialNumber { get; set; }
    public string? ChassisType { get; set; }
    public string? ChassisAssetTag { get; set; }
    public string? SystemSkuNumber { get; set; }
    public string? SystemFamily { get; set; }
}

public class SoftwareInfo
{
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
}

public class UsersGroupsInfo
{
    public string? LocalUsersJson { get; set; }
    public string? LoggedOnUsersJson { get; set; }
}

public class PeripheralsInfo
{
    public string? PrintersJson { get; set; }
    public string? UsbDevicesJson { get; set; }
    public string? AudioDevicesJson { get; set; }
}

public class EventLogsInfo
{
    public string? RecentErrorsJson { get; set; }
    public string? RecentWarningsJson { get; set; }
    public int? BlueScreenCount { get; set; }
    public DateTime? LastBlueScreen { get; set; }
}

public class PerformanceInfo
{
    public double? CpuUsagePercent { get; set; }
    public double? MemoryUsagePercent { get; set; }
    public double? DiskUsagePercent { get; set; }
}

public class HardwareIdsInfo
{
    public string? MachineGuid { get; set; }
    public string? ProductId { get; set; }
}

// === Helper Models ===

public class MemoryModuleInfo
{
    public string? Manufacturer { get; set; }
    public string? PartNumber { get; set; }
    public long Capacity { get; set; }
    public int Speed { get; set; }
    public string? MemoryType { get; set; }
    public string? FormFactor { get; set; }
    public string? DeviceLocator { get; set; }
    public string? BankLabel { get; set; }
    public string? SerialNumber { get; set; }
}

public class DiskInfoModel
{
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? InterfaceType { get; set; }
    public string? MediaType { get; set; }
    public long Size { get; set; }
    public int Partitions { get; set; }
    public string? Status { get; set; }
    public string? FirmwareRevision { get; set; }
}

public class PartitionInfoModel
{
    public int DiskIndex { get; set; }
    public int PartitionIndex { get; set; }
    public string? Type { get; set; }
    public long Size { get; set; }
    public bool Bootable { get; set; }
    public bool PrimaryPartition { get; set; }
}

public class LogicalDriveInfoModel
{
    public string? DriveLetter { get; set; }
    public string? Label { get; set; }
    public string? FileSystem { get; set; }
    public long Size { get; set; }
    public long FreeSpace { get; set; }
    public string? DriveType { get; set; }
}

public class GraphicsCardInfoModel
{
    public string? Name { get; set; }
    public string? Manufacturer { get; set; }
    public string? DriverVersion { get; set; }
    public DateTime? DriverDate { get; set; }
    public long VideoRam { get; set; }
    public string? Resolution { get; set; }
    public int RefreshRate { get; set; }
    public string? VideoProcessor { get; set; }
}

public class MonitorInfoModel
{
    public string? Name { get; set; }
    public string? Manufacturer { get; set; }
    public int ScreenWidth { get; set; }
    public int ScreenHeight { get; set; }
    public string? MonitorType { get; set; }
}

public class NetworkAdapterInfoModel
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? MacAddress { get; set; }
    public string? IpAddresses { get; set; }
    public string? SubnetMask { get; set; }
    public string? DefaultGateway { get; set; }
    public string? DnsServers { get; set; }
    public string? DhcpServer { get; set; }
    public bool DhcpEnabled { get; set; }
    public long Speed { get; set; }
    public string? Status { get; set; }
    public string? ConnectionType { get; set; }
}

public class InstalledSoftwareInfoModel
{
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Publisher { get; set; }
    public DateTime? InstallDate { get; set; }
    public string? InstallLocation { get; set; }
    public long? Size { get; set; }
}

public class WindowsUpdateInfoModel
{
    public string? HotFixId { get; set; }
    public string? Description { get; set; }
    public string? InstalledBy { get; set; }
    public DateTime? InstalledOn { get; set; }
}

public class ProcessInfoModel
{
    public int ProcessId { get; set; }
    public string? Name { get; set; }
    public string? Path { get; set; }
    public long WorkingSetSize { get; set; }
    public DateTime? StartTime { get; set; }
}

public class ServiceInfoModel
{
    public string? Name { get; set; }
    public string? DisplayName { get; set; }
    public string? Status { get; set; }
    public string? StartType { get; set; }
}

public class StartupProgramInfoModel
{
    public string? Name { get; set; }
    public string? Command { get; set; }
    public string? Location { get; set; }
    public string? User { get; set; }
}

public class LocalUserInfoModel
{
    public string? Name { get; set; }
    public string? FullName { get; set; }
    public string? Description { get; set; }
    public bool Disabled { get; set; }
    public bool PasswordRequired { get; set; }
    public bool PasswordChangeable { get; set; }
    public string? Sid { get; set; }
}

public class PrinterInfoModel
{
    public string? Name { get; set; }
    public string? DriverName { get; set; }
    public string? PortName { get; set; }
    public string? Status { get; set; }
    public bool Shared { get; set; }
    public bool Default { get; set; }
    public bool Network { get; set; }
}

public class UsbDeviceInfoModel
{
    public string? Name { get; set; }
    public string? DeviceId { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
}

public class EventLogEntryModel
{
    public DateTime TimeGenerated { get; set; }
    public string? Source { get; set; }
    public string? Message { get; set; }
    public int EventId { get; set; }
    public string? EntryType { get; set; }
}

public class ScheduledTaskInfoModel
{
    public string? Name { get; set; }
    public string? Path { get; set; }
    public string? Status { get; set; }
    public string? Author { get; set; }
}

public class SharedFolderInfoModel
{
    public string? Name { get; set; }
    public string? Path { get; set; }
    public string? Description { get; set; }
    public int MaximumAllowed { get; set; }
}
