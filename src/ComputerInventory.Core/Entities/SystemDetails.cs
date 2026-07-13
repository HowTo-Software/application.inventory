using System.ComponentModel.DataAnnotations.Schema;
using ComputerInventory.Core.Interfaces;

namespace ComputerInventory.Core.Entities;

/// <summary>
/// Parent record for system details collection (3NF).
/// Holds collection metadata. Detailed data lives in 16 sub-entities
/// organized into four domain groups (see SystemDetailsQueryExtensions for include helpers):
///   Hardware  — Bios, Motherboard, Cpu, Memory, Storage, Gpu, Battery, Chassis
///   OS        — Os, Security, Network
///   Software  — Software, UsersGroups
///   Diagnostics — Performance, EventLogs, Peripherals
/// </summary>
public class SystemDetails : ISoftDeletable
{
    public int Id { get; set; }
    public int ComputerId { get; set; }
    public Computer Computer { get; set; } = null!;
    
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
    public string? MachineGuid { get; set; }
    public string? ProductId { get; set; }
    
    // Concurrency token
    public byte[]? RowVersion { get; set; }

    // Soft-delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    // ── Hardware (physical components) ──────────────────────────
    [InverseProperty(nameof(SystemDetailBios.SystemDetail))]
    public SystemDetailBios? Bios { get; set; }
    [InverseProperty(nameof(SystemDetailMotherboard.SystemDetail))]
    public SystemDetailMotherboard? Motherboard { get; set; }
    [InverseProperty(nameof(SystemDetailCpu.SystemDetail))]
    public SystemDetailCpu? Cpu { get; set; }
    [InverseProperty(nameof(SystemDetailMemory.SystemDetail))]
    public SystemDetailMemory? Memory { get; set; }
    [InverseProperty(nameof(SystemDetailStorage.SystemDetail))]
    public SystemDetailStorage? Storage { get; set; }
    [InverseProperty(nameof(SystemDetailGpu.SystemDetail))]
    public SystemDetailGpu? Gpu { get; set; }
    [InverseProperty(nameof(SystemDetailBattery.SystemDetail))]
    public SystemDetailBattery? Battery { get; set; }
    [InverseProperty(nameof(SystemDetailChassis.SystemDetail))]
    public SystemDetailChassis? Chassis { get; set; }

    // ── Operating System & Environment ─────────────────────────
    [InverseProperty(nameof(SystemDetailOs.SystemDetail))]
    public SystemDetailOs? Os { get; set; }
    [InverseProperty(nameof(SystemDetailSecurity.SystemDetail))]
    public SystemDetailSecurity? Security { get; set; }
    [InverseProperty(nameof(SystemDetailNetwork.SystemDetail))]
    public SystemDetailNetwork? Network { get; set; }

    // ── Installed Software & Users ─────────────────────────────
    [InverseProperty(nameof(SystemDetailSoftware.SystemDetail))]
    public SystemDetailSoftware? Software { get; set; }
    [InverseProperty(nameof(SystemDetailUsersGroups.SystemDetail))]
    public SystemDetailUsersGroups? UsersGroups { get; set; }

    // ── Runtime Diagnostics ────────────────────────────────────
    [InverseProperty(nameof(SystemDetailPerformance.SystemDetail))]
    public SystemDetailPerformance? Performance { get; set; }
    [InverseProperty(nameof(SystemDetailEventLogs.SystemDetail))]
    public SystemDetailEventLogs? EventLogs { get; set; }
    [InverseProperty(nameof(SystemDetailPeripherals.SystemDetail))]
    public SystemDetailPeripherals? Peripherals { get; set; }
}

// Helper classes for JSON serialization
public class MemoryModule
{
    public string? Manufacturer { get; set; }
    public string? PartNumber { get; set; }
    public long? Capacity { get; set; }
    public int? Speed { get; set; }
    public string? MemoryType { get; set; }
    public string? FormFactor { get; set; }
    public string? DeviceLocator { get; set; }
    public string? BankLabel { get; set; }
    public string? SerialNumber { get; set; }
}

public class DiskInfo
{
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? InterfaceType { get; set; }
    public string? MediaType { get; set; }
    public long? Size { get; set; }
    public int? Partitions { get; set; }
    public string? Status { get; set; }
    public string? FirmwareRevision { get; set; }
}

public class PartitionInfo
{
    public int DiskIndex { get; set; }
    public int PartitionIndex { get; set; }
    public string? Type { get; set; }
    public long? Size { get; set; }
    public bool Bootable { get; set; }
    public bool PrimaryPartition { get; set; }
}

public class LogicalDriveInfo
{
    public string? DriveLetter { get; set; }
    public string? Label { get; set; }
    public string? FileSystem { get; set; }
    public long? Size { get; set; }
    public long? FreeSpace { get; set; }
    public string? DriveType { get; set; }
}

public class GraphicsCardInfo
{
    public string? Name { get; set; }
    public string? Manufacturer { get; set; }
    public string? DriverVersion { get; set; }
    public DateTime? DriverDate { get; set; }
    public long? VideoRam { get; set; }
    public string? Resolution { get; set; }
    public int? RefreshRate { get; set; }
    public string? VideoProcessor { get; set; }
}

public class MonitorInfo
{
    public string? Name { get; set; }
    public string? Manufacturer { get; set; }
    public string? SerialNumber { get; set; }
    public int? ScreenWidth { get; set; }
    public int? ScreenHeight { get; set; }
    public string? MonitorType { get; set; }
}

public class NetworkAdapterInfo
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Manufacturer { get; set; }
    public string? MacAddress { get; set; }
    public string? IpAddresses { get; set; }
    public string? SubnetMask { get; set; }
    public string? DefaultGateway { get; set; }
    public string? DnsServers { get; set; }
    public string? DhcpServer { get; set; }
    public bool? DhcpEnabled { get; set; }
    public long? Speed { get; set; }
    public string? Status { get; set; }
    public string? ConnectionType { get; set; }
}

public class InstalledSoftwareInfo
{
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Publisher { get; set; }
    public DateTime? InstallDate { get; set; }
    public string? InstallLocation { get; set; }
    public long? Size { get; set; }
}

public class WindowsUpdateInfo
{
    public string? HotFixId { get; set; }
    public string? Description { get; set; }
    public DateTime? InstalledOn { get; set; }
    public string? InstalledBy { get; set; }
}

public class ProcessInfo
{
    public int ProcessId { get; set; }
    public string? Name { get; set; }
    public string? Path { get; set; }
    public string? CommandLine { get; set; }
    public string? Owner { get; set; }
    public long? WorkingSetSize { get; set; }
    public DateTime? StartTime { get; set; }
    public double? CpuPercent { get; set; }
}

public class ServiceInfo
{
    public string? Name { get; set; }
    public string? DisplayName { get; set; }
    public string? Status { get; set; }
    public string? StartType { get; set; }
    public string? Path { get; set; }
    public string? Account { get; set; }
    public string? Description { get; set; }
}

public class StartupProgramInfo
{
    public string? Name { get; set; }
    public string? Command { get; set; }
    public string? Location { get; set; }
    public string? User { get; set; }
}

public class LocalUserInfo
{
    public string? Name { get; set; }
    public string? FullName { get; set; }
    public string? Description { get; set; }
    public bool Disabled { get; set; }
    public bool PasswordRequired { get; set; }
    public bool PasswordChangeable { get; set; }
    public DateTime? LastLogon { get; set; }
    public string? Sid { get; set; }
}

public class PrinterInfo
{
    public string? Name { get; set; }
    public string? DriverName { get; set; }
    public string? PortName { get; set; }
    public string? Status { get; set; }
    public bool Shared { get; set; }
    public bool Default { get; set; }
    public bool Network { get; set; }
}

public class UsbDeviceInfo
{
    public string? Name { get; set; }
    public string? DeviceId { get; set; }
    public string? Manufacturer { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
}

public class EventLogEntry
{
    public DateTime? TimeGenerated { get; set; }
    public string? Source { get; set; }
    public string? Message { get; set; }
    public int? EventId { get; set; }
    public string? EntryType { get; set; }
}

public class ScheduledTaskInfo
{
    public string? Name { get; set; }
    public string? Path { get; set; }
    public string? Status { get; set; }
    public DateTime? LastRunTime { get; set; }
    public DateTime? NextRunTime { get; set; }
    public string? Author { get; set; }
}

public class SharedFolderInfo
{
    public string? Name { get; set; }
    public string? Path { get; set; }
    public string? Description { get; set; }
    public int? MaximumAllowed { get; set; }
}
