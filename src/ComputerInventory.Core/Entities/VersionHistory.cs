using System.ComponentModel.DataAnnotations;
using ComputerInventory.Core.Interfaces;

namespace ComputerInventory.Core.Entities;

public class VersionHistory : ISoftDeletable
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Version { get; set; } = string.Empty;
    
    // FK to VERSION_TYPES lookup
    public int TypeId { get; set; }

    [Required]
    public string ChangeLog { get; set; } = string.Empty;

    public DateTime ReleasedAt { get; set; }

    [StringLength(100)]
    public string? ReleasedBy { get; set; }

    public bool IsCurrentVersion { get; set; }

    [StringLength(128)]
    public string? Checksum { get; set; }

    public long? FileSize { get; set; }
    
    // Soft-delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    // Navigation property
    public VersionTypeLookup TypeLookup { get; set; } = null!;
}

public enum VersionType
{
    Server,
    ThinClient
}
