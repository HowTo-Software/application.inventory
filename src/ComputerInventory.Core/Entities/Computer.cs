using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ComputerInventory.Core.Interfaces;

namespace ComputerInventory.Core.Entities;

public class Computer : ISoftDeletable, IAuditable
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string SerialNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string Hostname { get; set; } = string.Empty;

    [StringLength(45)]
    public string? IpAddress { get; set; }

    [StringLength(17)]
    public string? MacAddress { get; set; }
    
    // FK to COMPUTER_MODELS
    public int ModelId { get; set; }
    
    // FK to OPERATING_SYSTEMS
    public int? OperatingSystemId { get; set; }

    [StringLength(200)]
    public string? Processor { get; set; }
    public int? RamGB { get; set; }
    public int? StorageGB { get; set; }
    
    // FK to COMPUTER_TYPES lookup
    public int TypeId { get; set; }
    
    // FK to COMPUTER_STATUSES lookup
    public int StatusId { get; set; }
    
    // FK to LOCATIONS
    public int? LocationId { get; set; }
    
    // FK to DEPARTMENTS
    public int? DepartmentId { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
    public DateTime PurchaseDate { get; set; }
    public DateTime? WarrantyExpiration { get; set; }
    public decimal? PurchasePrice { get; set; }

    [StringLength(200)]
    public string? AssignedTo { get; set; }
    
    // Audit Fields
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Soft-delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }

    [StringLength(100)]
    public string? UpdatedBy { get; set; }
    
    /// <summary>
    /// Concurrency token for optimistic concurrency control.
    /// </summary>
    public byte[]? RowVersion { get; set; }
    
    // Navigation Properties
    public ComputerModel ComputerModel { get; set; } = null!;
    public OperatingSystemEntity? OperatingSystemEntity { get; set; }
    public ComputerTypeLookup TypeLookup { get; set; } = null!;
    public ComputerStatusLookup StatusLookup { get; set; } = null!;
    public Location? LocationEntity { get; set; }
    public Department? DepartmentEntity { get; set; }
    
    [InverseProperty(nameof(MaintenanceRecord.Computer))]
    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();
    [InverseProperty(nameof(ComputerHistory.Computer))]
    public ICollection<ComputerHistory> History { get; set; } = new List<ComputerHistory>();
    [InverseProperty(nameof(ComputerGeoLocation.Computer))]
    public ComputerGeoLocation? GeoLocation { get; set; }
    
    /// <summary>
    /// Agent connectivity, update tracking, and runtime metadata.
    /// Separated from core inventory data for cleaner domain boundaries.
    /// </summary>
    [InverseProperty(nameof(ComputerAgentStatus.Computer))]
    public ComputerAgentStatus AgentStatus { get; set; } = new();
}

public enum ComputerType
{
    Desktop,
    Laptop,
    Server,
    Workstation,
    Tablet,
    Other
}

public static class ComputerTypeParser
{
    public static ComputerType Parse(string? typeString)
    {
        if (string.IsNullOrEmpty(typeString))
            return ComputerType.Desktop;

        return typeString.ToLowerInvariant() switch
        {
            "desktop" => ComputerType.Desktop,
            "laptop" => ComputerType.Laptop,
            "tablet" => ComputerType.Tablet,
            "server" => ComputerType.Server,
            "workstation" => ComputerType.Workstation,
            "allinone" => ComputerType.Desktop,
            "handheld" => ComputerType.Tablet,
            "dockingstation" => ComputerType.Other,
            _ => ComputerType.Desktop
        };
    }
}

public enum ComputerStatus
{
    Available,
    Assigned,
    InMaintenance,
    Retired,
    Lost,
    Stolen,
    Disposed
}


