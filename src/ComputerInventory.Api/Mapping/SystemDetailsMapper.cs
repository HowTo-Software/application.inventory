using ComputerInventory.Core.Entities;
using ComputerInventory.Contracts;
using ComputerInventory.Api.Controllers;

namespace ComputerInventory.Api.Mapping;

/// <summary>
/// Maps between SystemDetails request/entity/report types.
/// </summary>
public static class SystemDetailsMapper
{
    public static SystemDetails ToEntity(this SystemDetailsRequest request, int computerId) => new()
    {
        ComputerId = computerId,
        CollectedAt = DateTime.UtcNow,
        MachineGuid = request.MachineGuid,
        ProductId = request.ProductId,
        Bios = MapBios(request.Bios),
        Motherboard = MapMotherboard(request.Motherboard),
        Cpu = MapCpu(request.Cpu),
        Memory = MapMemory(request.Memory),
        Storage = MapStorage(request.Storage),
        Gpu = MapGpu(request.Gpu),
        Network = MapNetwork(request.Network),
        Os = MapOs(request.Os),
        Security = MapSecurity(request.Security),
        Battery = MapBattery(request.Battery),
        Chassis = MapChassis(request.Chassis),
        Software = MapSoftware(request.Software),
        UsersGroups = MapUsersGroups(request.UsersGroups),
        Peripherals = MapPeripherals(request.Peripherals),
        EventLogs = MapEventLogs(request.EventLogs),
        Performance = MapPerformance(request.Performance)
    };

    public static SystemDetailsReport ToReport(this SystemDetails d) => new()
    {
        Id = d.Id,
        ComputerId = d.ComputerId,
        ComputerName = d.Computer?.Hostname ?? "Unknown",
        SerialNumber = d.Computer?.SerialNumber,
        CollectedAt = d.CollectedAt,

        // BIOS
        BiosVersion = d.Bios?.BiosVersion,
        BiosManufacturer = d.Bios?.BiosManufacturer,
        BiosSerialNumber = d.Bios?.BiosSerialNumber,
        SmbiosVersion = d.Bios?.SmbiosVersion,
        BiosReleaseDate = d.Bios?.BiosReleaseDate,

        // Motherboard
        MotherboardManufacturer = d.Motherboard?.MotherboardManufacturer,
        MotherboardProduct = d.Motherboard?.MotherboardProduct,
        MotherboardSerialNumber = d.Motherboard?.MotherboardSerialNumber,
        MotherboardVersion = d.Motherboard?.MotherboardVersion,

        // CPU
        CpuName = d.Cpu?.CpuName,
        CpuManufacturer = d.Cpu?.CpuManufacturer,
        CpuCores = d.Cpu?.CpuCores,
        CpuLogicalProcessors = d.Cpu?.CpuLogicalProcessors,
        CpuMaxClockSpeed = d.Cpu?.CpuMaxClockSpeed,
        CpuCurrentClockSpeed = d.Cpu?.CpuCurrentClockSpeed,
        CpuL2CacheSize = d.Cpu?.CpuL2CacheSize,
        CpuL3CacheSize = d.Cpu?.CpuL3CacheSize,
        CpuSocketDesignation = d.Cpu?.CpuSocketDesignation,
        CpuArchitecture = d.Cpu?.CpuArchitecture,
        CpuProcessorId = d.Cpu?.CpuProcessorId,

        // Memory
        TotalPhysicalMemory = d.Memory?.TotalPhysicalMemory,
        AvailablePhysicalMemory = d.Memory?.AvailablePhysicalMemory,
        TotalVirtualMemory = d.Memory?.TotalVirtualMemory,
        AvailableVirtualMemory = d.Memory?.AvailableVirtualMemory,
        MemorySlots = d.Memory?.MemorySlots,
        MemorySlotsUsed = d.Memory?.MemorySlotsUsed,
        MemoryModulesJson = d.Memory?.MemoryModulesJson,

        // Storage
        DisksJson = d.Storage?.DisksJson,
        PartitionsJson = d.Storage?.PartitionsJson,
        LogicalDrivesJson = d.Storage?.LogicalDrivesJson,
        TotalDiskSpace = d.Storage?.TotalDiskSpace,
        FreeDiskSpace = d.Storage?.FreeDiskSpace,

        // GPU
        GpuName = d.Gpu?.GpuName,
        GpuManufacturer = d.Gpu?.GpuManufacturer,
        GpuDriverVersion = d.Gpu?.GpuDriverVersion,
        GpuDriverDate = d.Gpu?.GpuDriverDate,
        GpuRam = d.Gpu?.GpuRam,
        GpuResolution = d.Gpu?.GpuResolution,
        GraphicsCardsJson = d.Gpu?.GraphicsCardsJson,

        // Monitors
        MonitorCount = d.Gpu?.MonitorCount,
        MonitorsJson = d.Gpu?.MonitorsJson,

        // Network
        NetworkAdaptersJson = d.Network?.NetworkAdaptersJson,
        DefaultGateway = d.Network?.DefaultGateway,
        DnsServers = d.Network?.DnsServers,
        DhcpServer = d.Network?.DhcpServer,
        DomainName = d.Network?.DomainName,
        DnsHostname = d.Network?.DnsHostname,
        Workgroup = d.Network?.Workgroup,
        DomainJoined = d.Network?.DomainJoined,

        // OS
        OsName = d.Os?.OsName,
        OsVersion = d.Os?.OsVersion,
        OsBuild = d.Os?.OsBuild,
        OsArchitecture = d.Os?.OsArchitecture,
        OsSerialNumber = d.Os?.OsSerialNumber,
        OsServicePackMajor = d.Os?.OsServicePackMajor,
        OsServicePackMinor = d.Os?.OsServicePackMinor,
        OsLocale = d.Os?.OsLocale,
        OsLanguage = d.Os?.OsLanguage,
        OsInstallDate = d.Os?.OsInstallDate,
        OsLastBootTime = d.Os?.OsLastBootTime,
        OsUptime = d.Os?.OsUptime,
        TimeZone = d.Os?.TimeZone,

        // License
        WindowsProductId = d.Os?.WindowsProductId,
        OsProductKey = d.Os?.OsProductKey,
        WindowsRegisteredOwner = d.Os?.WindowsRegisteredOwner,
        WindowsRegisteredOrganization = d.Os?.WindowsRegisteredOrganization,
        WindowsIsActivated = d.Os?.WindowsIsActivated,
        WindowsActivationStatus = d.Os?.WindowsActivationStatus,

        // Software
        InstalledSoftwareJson = d.Software?.InstalledSoftwareJson,
        InstalledSoftwareCount = d.Software?.InstalledSoftwareCount,

        // Updates
        InstalledUpdatesJson = d.Software?.InstalledUpdatesJson,
        InstalledUpdatesCount = d.Software?.InstalledUpdatesCount,
        LastUpdateDate = d.Software?.LastUpdateDate,

        // Processes
        RunningProcessesJson = d.Software?.RunningProcessesJson,
        RunningProcessesCount = d.Software?.RunningProcessesCount,

        // Services
        ServicesJson = d.Software?.ServicesJson,
        RunningServicesCount = d.Software?.RunningServicesCount,
        StoppedServicesCount = d.Software?.StoppedServicesCount,

        // Startup
        StartupProgramsJson = d.Software?.StartupProgramsJson,
        StartupProgramsCount = d.Software?.StartupProgramsCount,

        // Users
        LocalUsersJson = d.UsersGroups?.LocalUsersJson,
        LoggedOnUsersJson = d.UsersGroups?.LoggedOnUsersJson,

        // Security
        AntivirusProduct = d.Security?.AntivirusProduct,
        AntivirusStatus = d.Security?.AntivirusStatus,
        FirewallStatus = d.Security?.FirewallStatus,
        TpmPresent = d.Security?.TpmPresent,
        TpmVersion = d.Security?.TpmVersion,
        SecureBootEnabled = d.Security?.SecureBootEnabled,
        UacEnabled = d.Security?.UacEnabled,
        BitLockerEnabled = d.Security?.BitLockerEnabled,
        BitLockerStatus = d.Security?.BitLockerStatus,

        // Peripherals
        PrintersJson = d.Peripherals?.PrintersJson,
        UsbDevicesJson = d.Peripherals?.UsbDevicesJson,
        AudioDevicesJson = d.Peripherals?.AudioDevicesJson,

        // Battery
        HasBattery = d.Battery?.HasBattery,
        BatteryChargePercent = d.Battery?.BatteryChargePercent,
        BatteryStatus = d.Battery?.BatteryStatus,
        BatteryEstimatedRuntime = d.Battery?.BatteryEstimatedRuntime,
        BatteryDesignCapacity = d.Battery?.BatteryDesignCapacity,
        BatteryFullChargeCapacity = d.Battery?.BatteryFullChargeCapacity,

        // Events
        RecentErrorsJson = d.EventLogs?.RecentErrorsJson,
        RecentWarningsJson = d.EventLogs?.RecentWarningsJson,
        BlueScreenCount = d.EventLogs?.BlueScreenCount,
        LastBlueScreen = d.EventLogs?.LastBlueScreen,

        // Performance
        CpuUsagePercent = d.Performance?.CpuUsagePercent,
        MemoryUsagePercent = d.Performance?.MemoryUsagePercent,
        DiskUsagePercent = d.Performance?.DiskUsagePercent,

        // Chassis
        ChassisManufacturer = d.Chassis?.ChassisManufacturer,
        ChassisSerialNumber = d.Chassis?.ChassisSerialNumber,
        ChassisType = d.Chassis?.ChassisType,
        ChassisAssetTag = d.Chassis?.ChassisAssetTag,

        // Environment
        EnvironmentVariablesJson = d.Os?.EnvironmentVariablesJson,

        // Tasks
        ScheduledTasksJson = d.Software?.ScheduledTasksJson,
        ScheduledTasksCount = d.Software?.ScheduledTasksCount,

        // Shares
        SharedFoldersJson = d.Storage?.SharedFoldersJson,
        MappedDrivesJson = d.Storage?.MappedDrivesJson,

        // Hardware IDs
        MachineGuid = d.MachineGuid,
        ProductId = d.ProductId,
        SystemSkuNumber = d.Chassis?.SystemSkuNumber,
        SystemFamily = d.Chassis?.SystemFamily
    };

    private static SystemDetailBios MapBios(BiosRequest? r) => new()
    {
        BiosVersion = r?.BiosVersion,
        BiosManufacturer = r?.BiosManufacturer,
        BiosReleaseDate = r?.BiosReleaseDate,
        BiosSerialNumber = r?.BiosSerialNumber,
        SmbiosVersion = r?.SmbiosVersion
    };

    private static SystemDetailMotherboard MapMotherboard(MotherboardRequest? r) => new()
    {
        MotherboardManufacturer = r?.MotherboardManufacturer,
        MotherboardProduct = r?.MotherboardProduct,
        MotherboardSerialNumber = r?.MotherboardSerialNumber,
        MotherboardVersion = r?.MotherboardVersion
    };

    private static SystemDetailCpu MapCpu(CpuRequest? r) => new()
    {
        CpuName = r?.CpuName,
        CpuManufacturer = r?.CpuManufacturer,
        CpuCores = r?.CpuCores,
        CpuLogicalProcessors = r?.CpuLogicalProcessors,
        CpuMaxClockSpeed = r?.CpuMaxClockSpeed,
        CpuCurrentClockSpeed = r?.CpuCurrentClockSpeed,
        CpuArchitecture = r?.CpuArchitecture,
        CpuL2CacheSize = r?.CpuL2CacheSize,
        CpuL3CacheSize = r?.CpuL3CacheSize,
        CpuSocketDesignation = r?.CpuSocketDesignation,
        CpuProcessorId = r?.CpuProcessorId
    };

    private static SystemDetailMemory MapMemory(MemoryRequest? r) => new()
    {
        TotalPhysicalMemory = r?.TotalPhysicalMemory,
        AvailablePhysicalMemory = r?.AvailablePhysicalMemory,
        TotalVirtualMemory = r?.TotalVirtualMemory,
        AvailableVirtualMemory = r?.AvailableVirtualMemory,
        MemorySlots = r?.MemorySlots,
        MemorySlotsUsed = r?.MemorySlotsUsed,
        MemoryModulesJson = r?.MemoryModulesJson
    };

    private static SystemDetailStorage MapStorage(StorageRequest? r) => new()
    {
        DisksJson = r?.DisksJson,
        PartitionsJson = r?.PartitionsJson,
        LogicalDrivesJson = r?.LogicalDrivesJson,
        TotalDiskSpace = r?.TotalDiskSpace,
        FreeDiskSpace = r?.FreeDiskSpace,
        SharedFoldersJson = r?.SharedFoldersJson,
        MappedDrivesJson = r?.MappedDrivesJson
    };

    private static SystemDetailGpu MapGpu(GpuRequest? r) => new()
    {
        GpuName = r?.GpuName,
        GpuManufacturer = r?.GpuManufacturer,
        GpuDriverVersion = r?.GpuDriverVersion,
        GpuDriverDate = r?.GpuDriverDate,
        GpuRam = r?.GpuRam,
        GpuResolution = r?.GpuResolution,
        GraphicsCardsJson = r?.GraphicsCardsJson,
        MonitorsJson = r?.MonitorsJson,
        MonitorCount = r?.MonitorCount
    };

    private static SystemDetailNetwork MapNetwork(NetworkRequest? r) => new()
    {
        NetworkAdaptersJson = r?.NetworkAdaptersJson,
        DomainName = r?.DomainName,
        DnsHostname = r?.DnsHostname,
        Workgroup = r?.Workgroup,
        DomainJoined = r?.DomainJoined,
        DefaultGateway = r?.DefaultGateway,
        DnsServers = r?.DnsServers,
        DhcpServer = r?.DhcpServer
    };

    private static SystemDetailOs MapOs(OsRequest? r) => new()
    {
        OsName = r?.OsName,
        OsVersion = r?.OsVersion,
        OsBuild = r?.OsBuild,
        OsArchitecture = r?.OsArchitecture,
        OsInstallDate = r?.OsInstallDate,
        OsLastBootTime = r?.OsLastBootTime,
        OsSerialNumber = r?.OsSerialNumber,
        OsProductKey = r?.OsProductKey,
        OsServicePackMajor = r?.OsServicePackMajor,
        OsServicePackMinor = r?.OsServicePackMinor,
        OsLocale = r?.OsLocale,
        OsLanguage = r?.OsLanguage,
        TimeZone = r?.TimeZone,
        OsUptime = r?.OsUptime,
        WindowsProductId = r?.WindowsProductId,
        WindowsRegisteredOwner = r?.WindowsRegisteredOwner,
        WindowsRegisteredOrganization = r?.WindowsRegisteredOrganization,
        WindowsIsActivated = r?.WindowsIsActivated,
        WindowsActivationStatus = r?.WindowsActivationStatus,
        EnvironmentVariablesJson = r?.EnvironmentVariablesJson
    };

    private static SystemDetailSecurity MapSecurity(SecurityRequest? r) => new()
    {
        AntivirusProduct = r?.AntivirusProduct,
        AntivirusStatus = r?.AntivirusStatus,
        FirewallStatus = r?.FirewallStatus,
        BitLockerEnabled = r?.BitLockerEnabled,
        BitLockerStatus = r?.BitLockerStatus,
        SecureBootEnabled = r?.SecureBootEnabled,
        TpmPresent = r?.TpmPresent,
        TpmVersion = r?.TpmVersion,
        UacEnabled = r?.UacEnabled
    };

    private static SystemDetailBattery MapBattery(BatteryRequest? r) => new()
    {
        HasBattery = r?.HasBattery,
        BatteryChargePercent = r?.BatteryChargePercent,
        BatteryStatus = r?.BatteryStatus,
        BatteryEstimatedRuntime = r?.BatteryEstimatedRuntime,
        BatteryHealth = r?.BatteryHealth,
        BatteryCycleCount = r?.BatteryCycleCount,
        BatteryDesignCapacity = r?.BatteryDesignCapacity,
        BatteryFullChargeCapacity = r?.BatteryFullChargeCapacity
    };

    private static SystemDetailChassis MapChassis(ChassisRequest? r) => new()
    {
        SystemSkuNumber = r?.SystemSkuNumber,
        SystemFamily = r?.SystemFamily,
        ChassisType = r?.ChassisType,
        ChassisManufacturer = r?.ChassisManufacturer,
        ChassisSerialNumber = r?.ChassisSerialNumber,
        ChassisAssetTag = r?.ChassisAssetTag
    };

    private static SystemDetailSoftware MapSoftware(SoftwareRequest? r) => new()
    {
        InstalledSoftwareJson = r?.InstalledSoftwareJson,
        InstalledSoftwareCount = r?.InstalledSoftwareCount,
        InstalledUpdatesJson = r?.InstalledUpdatesJson,
        InstalledUpdatesCount = r?.InstalledUpdatesCount,
        LastUpdateDate = r?.LastUpdateDate,
        RunningProcessesJson = r?.RunningProcessesJson,
        RunningProcessesCount = r?.RunningProcessesCount,
        ServicesJson = r?.ServicesJson,
        RunningServicesCount = r?.RunningServicesCount,
        StoppedServicesCount = r?.StoppedServicesCount,
        StartupProgramsJson = r?.StartupProgramsJson,
        StartupProgramsCount = r?.StartupProgramsCount,
        ScheduledTasksJson = r?.ScheduledTasksJson,
        ScheduledTasksCount = r?.ScheduledTasksCount
    };

    private static SystemDetailUsersGroups MapUsersGroups(UsersGroupsRequest? r) => new()
    {
        LocalUsersJson = r?.LocalUsersJson,
        LocalGroupsJson = r?.LocalGroupsJson,
        LoggedOnUsersJson = r?.LoggedOnUsersJson
    };

    private static SystemDetailPeripherals MapPeripherals(PeripheralsRequest? r) => new()
    {
        PrintersJson = r?.PrintersJson,
        UsbDevicesJson = r?.UsbDevicesJson,
        AudioDevicesJson = r?.AudioDevicesJson
    };

    private static SystemDetailEventLogs MapEventLogs(EventLogsRequest? r) => new()
    {
        RecentErrorsJson = r?.RecentErrorsJson,
        RecentWarningsJson = r?.RecentWarningsJson,
        BlueScreenCount = r?.BlueScreenCount,
        LastBlueScreen = r?.LastBlueScreen
    };

    private static SystemDetailPerformance MapPerformance(PerformanceRequest? r) => new()
    {
        CpuUsagePercent = r?.CpuUsagePercent,
        MemoryUsagePercent = r?.MemoryUsagePercent,
        DiskUsagePercent = r?.DiskUsagePercent
    };
}
