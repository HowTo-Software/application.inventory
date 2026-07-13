using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ComputerInventory.Core.Interfaces;

namespace ComputerInventory.Core.Entities;

/// <summary>
/// Restore point metadata (3NF).
/// System info in RestorePointSystemInfo, JSON snapshots in RestorePointSnapshot.
/// </summary>
public class RestorePoint : ISoftDeletable
{
    public int Id { get; set; }
    public int ComputerId { get; set; }
    public Computer Computer { get; set; } = null!;
    
    public Guid RestorePointId { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // FK to RESTORE_POINT_TYPES lookup
    public int TypeId { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public long? DataSizeBytes { get; set; }

    [StringLength(128)]
    public string? DataHash { get; set; }

    public bool IsComplete { get; set; }

    [StringLength(2000)]
    public string? ErrorMessage { get; set; }
    
    // Soft-delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    // Navigation properties
    public RestorePointTypeLookup TypeLookup { get; set; } = null!;
    [InverseProperty(nameof(RestorePointSystemInfo.RestorePoint))]
    public RestorePointSystemInfo? SystemInfo { get; set; }
    [InverseProperty(nameof(RestorePointSnapshot.RestorePoint))]
    public RestorePointSnapshot? Snapshot { get; set; }
}

public enum RestorePointType
{
    Reboot = 0,
    Scheduled = 1,
    Manual = 2,
    Initial = 3,
    PreChange = 4,
    PostChange = 5
}
