using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ComputerInventory.Core.Interfaces;

namespace ComputerInventory.Core.Entities;

public class MaintenanceRecord : ISoftDeletable, IHasCreationAudit
{
    public int Id { get; set; }
    public int ComputerId { get; set; }
    [InverseProperty(nameof(Entities.Computer.MaintenanceRecords))]
    public Computer Computer { get; set; } = null!;
    
    // FK to MAINTENANCE_TYPES lookup
    public int TypeId { get; set; }

    [Required]
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    public DateTime ScheduledDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public decimal? Cost { get; set; }

    [StringLength(100)]
    public string? PerformedBy { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
    
    // FK to MAINTENANCE_STATUSES lookup
    public int StatusId { get; set; }
    
    // Audit Fields
    public DateTime CreatedAt { get; set; }

    [StringLength(100)]
    public string? CreatedBy { get; set; }
    
    // Concurrency token
    public byte[]? RowVersion { get; set; }

    // Soft-delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    // Navigation Properties
    public MaintenanceTypeLookup TypeLookup { get; set; } = null!;
    public MaintenanceStatusLookup StatusLookup { get; set; } = null!;
}

public enum MaintenanceType
{
    Repair,
    Upgrade,
    Cleaning,
    SoftwareUpdate,
    HardwareReplacement,
    Inspection,
    Other
}

public enum MaintenanceStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled
}
