using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ComputerInventory.Core.Entities;

/// <summary>
/// Point-in-time system identification snapshot for a restore point.
/// Intentionally denormalized as a historical record of what the machine looked like.
/// Maps to RESTORE_POINT_SYSTEM_INFO table.
/// </summary>
public class RestorePointSystemInfo
{
    public int Id { get; set; }
    public int RestorePointId { get; set; }
    
    public DateTime? SystemBootTime { get; set; }
    public long? UptimeSeconds { get; set; }

    [StringLength(100)]
    public string? Hostname { get; set; }

    [StringLength(100)]
    public string? SerialNumber { get; set; }

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(17)]
    public string? MacAddress { get; set; }

    [StringLength(100)]
    public string? Manufacturer { get; set; }

    [StringLength(100)]
    public string? Model { get; set; }

    [StringLength(100)]
    public string? CurrentUser { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation
    [InverseProperty(nameof(Entities.RestorePoint.SystemInfo))]
    public RestorePoint RestorePoint { get; set; } = null!;
}

/// <summary>
/// JSON data collections captured at restore point time.
/// Maps to RESTORE_POINT_SNAPSHOTS table.
/// </summary>
public class RestorePointSnapshot
{
    public int Id { get; set; }
    public int RestorePointId { get; set; }
    
    public string? SystemDetailsJson { get; set; }
    public string? InstalledSoftwareJson { get; set; }
    public string? RunningProcessesJson { get; set; }
    public string? WindowsServicesJson { get; set; }
    public string? StartupProgramsJson { get; set; }
    public string? ScheduledTasksJson { get; set; }
    public string? EnvironmentVariablesJson { get; set; }
    public string? NetworkConfigJson { get; set; }
    public string? StorageConfigJson { get; set; }
    public string? EventLogSummaryJson { get; set; }
    public string? WindowsUpdatesJson { get; set; }
    public string? DeviceDriversJson { get; set; }
    public string? FirewallRulesJson { get; set; }
    public string? LocalUsersJson { get; set; }
    public string? LocalGroupsJson { get; set; }
    public string? SharedFoldersJson { get; set; }
    public string? PrintersJson { get; set; }
    public string? UsbDevicesJson { get; set; }
    public string? BatteryStatusJson { get; set; }
    public string? BitLockerStatusJson { get; set; }
    public string? SecurityStatusJson { get; set; }

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation
    [InverseProperty(nameof(Entities.RestorePoint.Snapshot))]
    public RestorePoint RestorePoint { get; set; } = null!;
}
